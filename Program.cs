using System;
using Microsoft.Win32;
using System.Data.Odbc;
using System.IO;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Data.SqlClient;
using BLTrapeze_v04;
//using Microsoft.Win32;

namespace BLTrapeze_v0_4
{
    class Program
    {
        static void Main(string[] args)
        {

            Logger.Initialize(); // uruchomienie loggera
            Logger.WriteLine("Program został uruchomiony.");

            // Tworzymy lub aktualizujemy DSN do Paradox
            CreateExactParadoxDsn();


            // test zapytania z wykorzystaniem metody
            // Zapytanie testowe do widoku vCityCardBlackList
            string sqlQuery = @"
            SELECT TOP (10) *
                  ,[DeactivationReason]
            FROM [CityCard].[dbo].[vCityCardBlackList]
            ORDER BY DeactivationDate DESC";

            try
            {
                // Otwórz połączenie do bazy SQL za pomocą funkcji GetSqlConnectionFromConfig()
                using (SqlConnection connection = GetSqlConnectionFromConfig())
                {
                    // Utwórz polecenie SQL na podstawie zapytania i połączenia
                    using (SqlCommand command = new SqlCommand(sqlQuery, connection))
                    {
                        // Wykonaj zapytanie i pobierz wyniki
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            Console.WriteLine("Wyniki zapytania:\n");

                            // Wypisz nagłówki kolumn
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                Console.Write(reader.GetName(i) + "\t");
                            }
                            Console.WriteLine("\n" + new string('-', 50));

                            // Wypisz wiersze z danych
                            while (reader.Read())
                            {
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    Console.Write(reader[i]?.ToString() + "\t");
                                }
                                Console.WriteLine(); // nowa linia po każdym wierszu
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Obsługa błędu połączenia lub zapytania
                Console.WriteLine("Błąd podczas wykonywania zapytania:");
                Console.WriteLine(ex.Message);
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
            string dbFile = @"D:\BLTrapeze\CardListTest.db";
            string pxFile = @"D:\BLTrapeze\CardListTest.px";
            string dbBackup = @"D:\BLTrapeze\CardListTest_backup.db";
            string pxBackup = @"D:\BLTrapeze\CardListTest_backup.px";

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
            string csvFile = @"D:\BLTrapeze\BLfull.csv";

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
                    Console.WriteLine("Połączono z bazą PARADOX.");

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
                            Console.WriteLine($"Wstawiono {rows} wiersz(y) z CSV (linia {i + 1}), CadrId: {cardId}.");
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
            Logger.WriteLine("Program zakończył działanie.");
            Console.ReadKey();
        }


        // Funkcja sprawdza, czy źródło danych ODBC o nazwie ParadoxT2 istnieje.
        // Jeśli nie istnieje — tworzy je. Jeśli istnieje — sprawdza i aktualizuje parametry.
        static void CreateExactParadoxDsn()
        {
            // Nazwa źródła danych ODBC (DSN)
            const string dsnName = "ParadoxT2";

            // Ścieżki rejestru do konfiguracji DSN
            string dsnKeyPath = $@"SOFTWARE\WOW6432Node\ODBC\ODBC.INI\{dsnName}";
            string odbcListPath = @"SOFTWARE\WOW6432Node\ODBC\ODBC.INI\ODBC Data Sources";

            try
            {
                // Usuń istniejący wpis DSN, jeśli występuje
                Registry.LocalMachine.DeleteSubKeyTree(dsnKeyPath, false);
                Logger.WriteLine($"Usunięto istniejący klucz rejestru: {dsnKeyPath}");

                using (var sourcesKey = Registry.LocalMachine.OpenSubKey(odbcListPath, writable: true))
                {
                    sourcesKey?.DeleteValue(dsnName, false);
                    Logger.WriteLine($"Usunięto wpis z listy źródeł danych ODBC: {dsnName}");
                }

                // Utwórz nowy wpis DSN z pełną konfiguracją
                using (var dsnKey = Registry.LocalMachine.CreateSubKey(dsnKeyPath))
                {
                    dsnKey.SetValue("CollatingSequence", "ASCII");
                    dsnKey.SetValue("Database", @"D:\BLTrapeze");
                    dsnKey.SetValue("DefaultDir", @"D:\BLTrapeze");
                    dsnKey.SetValue("Description", "ParadoxT2");
                    dsnKey.SetValue("Driver", @"C:\Program Files (x86)\Common Files\Borland Shared\BDE\IDAPI32.DLL");
                    dsnKey.SetValue("DriverId", "538");
                    dsnKey.SetValue("Exclusive", "FALSE");
                    dsnKey.SetValue("FIL", "Paradox 5.X");
                    dsnKey.SetValue("PageTimeout", "5");
                    dsnKey.SetValue("ParadoxNetPath", @"D:\BLTrapeze");
                    dsnKey.SetValue("SystemType", "3.x");
                    dsnKey.SetValue("UserName", "bborowic");

                    Logger.WriteLine($"Utworzono nowy wpis rejestru DSN: {dsnKeyPath}");
                }

                // Dodaj wpis do globalnej listy źródeł danych ODBC
                using (var listKey = Registry.LocalMachine.OpenSubKey(odbcListPath, writable: true))
                {
                    listKey?.SetValue(dsnName, "Microsoft Paradox Driver (*.db )");
                    Logger.WriteLine($"Zarejestrowano DSN '{dsnName}' w ODBC Data Sources.");
                }

                // Potwierdzenie zakończenia operacji
                Console.WriteLine("DSN 'ParadoxT2' został odtworzony 1:1 jak w działającym środowisku.");
                Logger.WriteLine("Zakończono konfigurację DSN 'ParadoxT2'.");
            }
            catch (Exception ex)
            {
                // W przypadku błędu zapisz szczegóły do logu
                Logger.WriteLine("Błąd podczas tworzenia DSN 'ParadoxT2': " + ex.Message);
                throw;
            }
        }


        static SqlConnection GetSqlConnectionFromConfig()
        {
            // Pobierz nazwę aktywnego połączenia z App.config
            string activeConnName = ConfigurationManager.AppSettings["ActiveConnection"];
            Logger.WriteLine($"Pobrano nazwę połączenia z App.config: {activeConnName}");

            // Pobierz dane połączenia z connectionStrings
            var connSettings = ConfigurationManager.ConnectionStrings[activeConnName];

            // Jeśli nie znaleziono połączenia – zapisz do logu i zgłoś wyjątek
            if (connSettings == null)
            {
                Logger.WriteLine("Błąd: nie znaleziono połączenia w App.config.");
                throw new InvalidOperationException("Nie znaleziono połączenia w App.config.");
            }

            // Pobierz connection string i utwórz połączenie SQL
            string connectionString = connSettings.ConnectionString;
            Logger.WriteLine("Utworzono obiekt SqlConnection.");

            try
            {
                // Otwórz połączenie do bazy
                var connection = new SqlConnection(connectionString);
                connection.Open();
                Logger.WriteLine("Połączenie z bazą SQL zostało nawiązane.");

                // Zwróć aktywne połączenie
                return connection;
            }
            catch (Exception ex)
            {
                // Jeśli wystąpił błąd – zapisz szczegóły do logu
                Logger.WriteLine("Błąd podczas łączenia z bazą SQL: " + ex.Message);
                throw;
            }
        }
    }


}