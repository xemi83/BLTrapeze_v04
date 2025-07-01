using System;
using Microsoft.Win32;
using System.Data.Odbc;
using System.IO;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Data.SqlClient;
//using Microsoft.Win32;

namespace BLTrapeze_v0_3
{
    class Program
    {
        static void Main(string[] args)
        {

            // dodawania wpisu do ODBC 32 PARADOX


            const string dsnName = "ParadoxT2";
            string baseKeyPath = $@"SOFTWARE\WOW6432Node\ODBC\ODBC.INI\{dsnName}";

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(baseKeyPath, writable: true))
            {
                if (key == null)
                {
                    Console.WriteLine($"DSN '{dsnName}' nie istnieje.");
                    return;
                }

                bool modified = false;

                // Parametry do sprawdzenia i ewentualnego nadpisania
                modified |= EnsureValue(key, "Driver", @"C:\Program Files (x86)\Common Files\Borland Shared\BDE\IDAPI32.DLL");
                modified |= EnsureValue(key, "ParadoxNetPath", @"D:\KKM33");
                modified |= EnsureValue(key, "Database", @"D:\KKM33");
                modified |= EnsureValue(key, "CollatingSequence", "ASCII");
                modified |= EnsureValue(key, "Exclusive", "FALSE");
                modified |= EnsureValue(key, "PageTimeout", "5");
                modified |= EnsureValue(key, "UserName", "bborowic");
                modified |= EnsureValue(key, "SystemType", "3.x");

                if (modified)
                {
                    Console.WriteLine("Zaktualizowano konfigurację DSN.");
                }
                else
                {
                    Console.WriteLine("DSN jest poprawnie skonfigurowany.");
                }
            }

            // Połączenie do MS SQL z danych z App.config

            string activeConnName = ConfigurationManager.AppSettings["ActiveConnection"];
            var connSettings = ConfigurationManager.ConnectionStrings[activeConnName];

            if (connSettings == null)
            {
                Console.WriteLine("Nie znaleziono połączenia w App.config.");
                return;
            }

            string connectionString = connSettings.ConnectionString;

            string sqlQuery = @"
            SELECT TOP (10) *
                  ,[DeactivationReason]
            FROM [CityCard].[dbo].[vCityCardBlackList]
            ORDER BY DeactivationDate DESC";

            using (SqlConnection connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand(sqlQuery, connection))
            {
                try
                {
                    connection.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        Console.WriteLine("Wyniki zapytania:\n");

                        // Wypisz kolumny
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            Console.Write(reader.GetName(i) + "\t");
                        }
                        Console.WriteLine("\n" + new string('-', 50));

                        // Wypisz wiersze
                        while (reader.Read())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                Console.Write(reader[i]?.ToString() + "\t");
                            }
                            Console.WriteLine();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Błąd podczas zapytania: " + ex.Message);
                }
            }

            // 1. Pobieranie numeru automatu

            string nr;
                        
            while (true)
            {
                Console.Write("Podaj numer automatu (maks. 3 cyfry): ");
                nr = Console.ReadLine();

                if (Regex.IsMatch(nr, @"^\d{1,3}$"))
                    break;
                else
                    Console.WriteLine("Nieprawidłowy numer. Podaj tylko cyfry (1–3 znaki).");
            }

            // 2. Tworzenie folderu
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
            string folderName = $"KKM{nr}_{timestamp}";
            string targetFolderPath = Path.Combine(Directory.GetCurrentDirectory(), folderName);

            try
            {
                Directory.CreateDirectory(targetFolderPath);
                Console.WriteLine($"Utworzono folder: {targetFolderPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd podczas tworzenia folderu: " + ex.Message);
                return;
            }

            // 3. Ścieżki plików
            string dbFile = @"D:\KKM33\CardListTest.db";
            string pxFile = @"D:\KKM33\CardListTest.px";
            string dbBackup = @"D:\KKM33\CardListTest_backup.db";
            string pxBackup = @"D:\KKM33\CardListTest_backup.px";

            // 4. Kopia zapasowa plików
            try
            {
                if (File.Exists(dbFile))
                {
                    File.Copy(dbFile, dbBackup, true);
                    Console.WriteLine("Utworzono kopię .db");
                }

                if (File.Exists(pxFile))
                {
                    File.Copy(pxFile, pxBackup, true);
                    Console.WriteLine("Utworzono kopię .px");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd przy tworzeniu kopii: " + ex.Message);
                return;
            }

            // 5. INSERT do bazy
            string connStr = "DSN=ParadoxT2;";
            string csvFile = @"D:\KKM33\BLfull.csv";
            //int limit = 1000; // <== ZMIENNA DO KONTROLI ILOŚCI WIERSZY

            int limit = 0;
            while (true)
            {
                Console.Write("Podaj liczbę wierszy do dodania (max 6 cyfr): ");
                string input = Console.ReadLine();

                // Sprawdź czy input to liczba i ma max 6 cyfr
                if (input.Length <= 6 && int.TryParse(input, out limit))
                {
                    break; // Poprawna liczba – wyjdź z pętli
                }
                else
                {
                    Console.WriteLine("Błąd: Wprowadź poprawną liczbę całkowitą (max 6 cyfr).");
                }
            }

            Console.WriteLine($"Dodanych zostanie {limit} wierszy.");

            try
            {
                using (OdbcConnection conn = new OdbcConnection(connStr))
                {
                    conn.Open();
                    Console.WriteLine("Połączono z bazą.");

                    string[] lines = File.ReadAllLines(csvFile);

                    // Pomijamy pierwszy wiersz (nagłówek)
                    for (int i = 1; i <= limit && i < lines.Length; i++)
                    {
                        string[] columns = lines[i].Split(';'); // <== UWAGA: separator to średnik

                        if (columns.Length < 4)
                        {
                            Console.WriteLine($"Wiersz {i + 1} pominięty - zbyt mało kolumn.");
                            continue;
                        }

                        string cardId = columns[1].Trim(); // z drugiej kolumny CSV
                        string customerId = columns[3].Trim(); // z czwartej kolumny CSV
                        string type = "1";

                        string rawDate = columns[2].Trim(); // z trzeciej kolumny CSV
                        string formattedDate;

                        if (DateTime.TryParse(rawDate, out DateTime dt))
                        {
                            formattedDate = dt.ToString("dd.MM.yyyy HH:mm:ss");
                        }
                        else
                        {
                            Console.WriteLine($"Błąd konwersji daty w wierszu {i + 1}: {rawDate}");
                            continue;
                        }

                        string query = $"INSERT INTO CardListTest (CardId, CustomerId, Type, Datum) " +
                                       $"VALUES('{cardId}', {customerId}, {type}, '{formattedDate}');";

                        using (OdbcCommand cmd = new OdbcCommand(query, conn))
                        {
                            int rows = cmd.ExecuteNonQuery();
                            Console.WriteLine($"Wstawiono {rows} wiersz(y) z CSV (linia {i + 1}).");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd podczas zapisu do bazy: " + ex.ToString());
                Console.WriteLine("Naciśnij klawisz aby kontynuować...");
                Console.ReadKey(); // by program nie zamknął się od razu
            }


            // 6. Przeniesienie zmodyfikowanych plików do nowego folderu
            try
            {
                File.Copy(dbFile, Path.Combine(targetFolderPath, Path.GetFileName(dbFile)), true);
                File.Copy(pxFile, Path.Combine(targetFolderPath, Path.GetFileName(pxFile)), true);
                Console.WriteLine("Skopiowano pliki do nowego katalogu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd kopiowania do folderu: " + ex.Message);
                return;
            }

            // 7. Przywrócenie plików z kopii
            try
            {
                File.Copy(dbBackup, dbFile, true);
                File.Copy(pxBackup, pxFile, true);
                Console.WriteLine("Przywrócono pliki źródłowe z backupu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd przywracania z backupu: " + ex.Message);
            }

            // 8. Usuwanie backupów
            try
            {
                if (File.Exists(dbBackup))
                {
                    File.Delete(dbBackup);
                    Console.WriteLine("Usunięto backup .db");
                }

                if (File.Exists(pxBackup))
                {
                    File.Delete(pxBackup);
                    Console.WriteLine("Usunięto backup .px");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd przy usuwaniu backupów: " + ex.Message);
            }

            Console.WriteLine("\nNaciśnij dowolny klawisz, aby zakończyć...");
            Console.ReadKey();
        }


        static bool EnsureValue(RegistryKey key, string name, string expectedValue)
        {
            var currentValue = key.GetValue(name)?.ToString();
            if (currentValue != expectedValue)
            {
                key.SetValue(name, expectedValue);
                Console.WriteLine($"Zmieniono {name}: '{currentValue}' → '{expectedValue}'");
                return true;
            }
            return false;
        }

    }


}