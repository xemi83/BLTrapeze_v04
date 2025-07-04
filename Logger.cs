using System;
using System.IO;
using System.Linq;
using System.Configuration;

namespace BLTrapeze_v04
{
    internal static class Logger
    {
        // Ścieżka do folderu z logami – pobrana z App.config (klucz LogDirectory)
        private static string logDirectory = ConfigurationManager.AppSettings["LogDirectory"];

        // Maksymalna liczba logów, jakie mogą zostać – starsze będą usuwane
        private static int maxLogFiles = 10;

        // Pełna ścieżka do aktualnego pliku logu (ustalana w Initialize)
        private static string logFilePath;

        // Metoda uruchamiana na początku programu – ustawia logger
        public static void Initialize()
        {
            // Jeśli wpis w App.config nie istnieje lub jest pusty – użyj domyślnej ścieżki
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                logDirectory = @"D:\BLTrapeze\Logs";
            }

            // Tworzy folder logów, jeśli nie istnieje
            Directory.CreateDirectory(logDirectory);

            // Tworzy nazwę pliku na podstawie aktualnej daty (jeden plik dziennie)
            string fileName = $"log_{DateTime.Now:yyyy-MM-dd}.txt";
            logFilePath = Path.Combine(logDirectory, fileName);

            // Zapisz pierwszą linię do logu
            WriteLine("=== Start logu ===");

            // Usuń najstarsze logi, jeśli jest ich za dużo
            CleanupOldLogs();
        }

        // Metoda zapisu wpisu do logu (do pliku i konsoli)
        public static void WriteLine(string message)
        {
            // Formatowana linia z timestampem
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";

            // Pokaż w konsoli
            Console.WriteLine(line);

            try
            {
                // Dopisz linię na końcu pliku
                File.AppendAllLines(logFilePath, new[] { line });
            }
            catch (Exception ex)
            {
                // Jeśli coś poszło nie tak – pokaż błąd w konsoli
                Console.WriteLine("Błąd zapisu do logu: " + ex.Message);
            }
        }

        // Usuwa stare pliki logów, pozostawiając tylko najnowsze (np. 10)
        private static void CleanupOldLogs()
        {
            try
            {
                // Znajdź wszystkie pliki log_*.txt w katalogu logów
                var files = new DirectoryInfo(logDirectory)
                    .GetFiles("log_*.txt")
                    .OrderByDescending(f => f.CreationTime) // od najnowszych
                    .Skip(maxLogFiles); // pomiń 10 najnowszych

                // Usuń pozostałe (czyli starsze)
                foreach (var file in files)
                    file.Delete();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Błąd czyszczenia logów: " + ex.Message);
            }
        }
    }
}
