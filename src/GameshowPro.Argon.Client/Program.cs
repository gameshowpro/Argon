using Argon;
using System.Reflection;
while (true)
{
    Console.ForegroundColor = ConsoleColor.White;
    Console.WriteLine($"Argon client utility {Assembly.GetCallingAssembly().GetName().Version?.ToString()}. Select function:");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(" 1. Ensure control");
    Console.WriteLine(" 2. Check control");
    Console.WriteLine(" 3. Release control");
    Console.WriteLine(" 4. Write client profile");
    Console.WriteLine(" 5. Test license.");
    Console.WriteLine(" 6. Check and update clock.");
    Console.WriteLine(" 7. Check clock.");
    Console.WriteLine(" 8. Set working directory");
    Console.WriteLine(" Q. Quit.");
    switch (Console.ReadKey(true).Key)
    {
        case ConsoleKey.D1:
            var resultA = ConfirmInitialization();
            Console.ForegroundColor = resultA.Code == InitializeResultCode.OK ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine("Result: {0}", resultA.Code);
            Console.WriteLine("{0}", resultA.Message);
            break;
        case ConsoleKey.D2:
            var resultB = CheckControl();
            Console.ForegroundColor = resultB.Code == InitializeResultCode.OK ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine("Result: {0}", resultB.Code);
            Console.WriteLine("{0}", resultB.Message);
            break;
        case ConsoleKey.D3:
            var resultC = ReleaseControl();
            Console.ForegroundColor = resultC.Code == InitializeResultCode.OK ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine("Result: {0}", resultC.Code);
            Console.WriteLine("{0}", resultC.Message);
            break;
        case ConsoleKey.D4:
            GetPublicKeyResult result = WritePublicKey(Path.Combine(GetWorkingDir(), "Profiles"));
            Console.ForegroundColor = result.Code == GetPublicKeyResultCode.Created ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine("Result: {0}", result.Code);
            Console.WriteLine("{0}", result.Message);
            break;
        case ConsoleKey.D5:
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Paste path to license file and press enter");
            Console.ForegroundColor = ConsoleColor.White;
            string? path = Console.ReadLine();
            if (path == null)
            {
                return;
            }
            path = path.Replace("\"", string.Empty);
            if (File.Exists(path))
            {
                CheckLicenseResult checkResult = CheckLicense(path); 
                Console.ForegroundColor = checkResult.IsOk ? ConsoleColor.Green : ConsoleColor.Red;

                Console.WriteLine("Result: {0}", checkResult.IsOk ? "OK" : "Fail");
                Console.WriteLine("{0} seconds", checkResult.Details);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("File not found");
            }
            break;
        case ConsoleKey.D6:
            Tools.CheckAndRachetTimeResult timeResult = CheckAndRachetTime();
            Console.ForegroundColor = timeResult.DateTimeOffset.HasValue ? ConsoleColor.Green : ConsoleColor.Red;
            if (timeResult.DateTimeOffset.HasValue)
            {
                Console.WriteLine("Result (in local time): {0:o}", timeResult.DateTimeOffset.Value.ToLocalTime());
            }
            if (timeResult.Delta.HasValue)
            {
                Console.WriteLine("Error: {0} seconds", timeResult.Delta.Value.TotalSeconds);
            }
            Console.WriteLine("{0}", timeResult.Message);
            break;
        case ConsoleKey.D7:
            Tools.CheckAndRachetTimeResult timeResult2 = CheckTime();
            Console.ForegroundColor = timeResult2.DateTimeOffset.HasValue ? ConsoleColor.Green : ConsoleColor.Red;
            if (timeResult2.DateTimeOffset.HasValue)
            {
                Console.WriteLine("Result (in local time): {0:o}", timeResult2.DateTimeOffset.Value.ToLocalTime());
            }
            if (timeResult2.Delta.HasValue)
            {
                Console.WriteLine("Error: {0} seconds", timeResult2.Delta.Value.TotalSeconds);
            }
            Console.WriteLine("{0}", timeResult2.Message);
            break;
        case ConsoleKey.D8:
            DoWorkingDirChangeUI();
            break;
        case ConsoleKey.Q:
            return;
    }
}