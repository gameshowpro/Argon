if (args.Length == 3)
{
    string profilePath = args[0];
    string outputDir = args[1];
    string payloadFile = args[2];

    string licenseFileName = Path.GetFileNameWithoutExtension(profilePath) + ".license";
    string licenseFilePath = Path.Combine(outputDir, licenseFileName);

    var result = GenerateLicense(payloadFile, profilePath, licenseFilePath);

    if (result.Code == GenerateLicenseResultCode.Written)
    {
        Console.WriteLine("License generated successfully: " + licenseFilePath);
        return 0;
    }
    else
    {
        Console.Error.WriteLine("Failed to generate license: " + result.Details);
        return 1;
    }
}

if (args.Length > 0)
{
    Console.WriteLine("Usage: Argon.Create <profilePath> <outputDirectory> <payloadFile>");
    return 1;
}

while (true)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"Argon client utility {GetProductVersion<Program>()}. Select function:");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(" 1. Write key pair");
    Console.WriteLine(" 2. Show key hash");
    Console.WriteLine(" 3. Generate license file");
    Console.WriteLine(" 4. Set working directory");
    Console.WriteLine(" Q. Quit.");
    switch (Console.ReadKey(true).Key)
    {
        case ConsoleKey.D1:
            var result = CreateKeyPair(null);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Result: {0}", result.Code);
            Console.WriteLine("{0}", result.Details);
            break;
        case ConsoleKey.D2:
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Hash of private key which is used to write license: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(GetPrivateHashString());
            break;
        case ConsoleKey.D3:
            Console.ForegroundColor = ConsoleColor.Green;
            string workingDir = GetWorkingDir();
            string profilesRoot = Path.Combine(workingDir, "Profiles");
            string licensesRoot = Path.Combine(workingDir, "Licenses");
            EnsureWorkingDirResult ensureResult = EnsureWorkingDir(workingDir);
            if (!ensureResult.Success)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ensureResult.Message);
                break;
            }
            HashSet<string> payloadExtensions = [".txt", ".json"];
            ImmutableList<WorkingFile> payloadFiles = [.. GetWorkingFiles(workingDir).Where(f => payloadExtensions.Contains(Path.GetExtension(f.FullPath)))];
            if (payloadFiles.IsEmpty)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"No files found in {workingDir}");
                break;
            }
            Console.WriteLine($"Listing files in {workingDir}");
            Console.WriteLine("Select license payload file by number: ");
            int i = 0;            
            foreach (WorkingFile filePath in payloadFiles)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(i++);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($". {filePath.Leaf}");
            }
            if (int.TryParse(Console.ReadLine(), out int payloadIndex) && payloadIndex >= 0 && payloadIndex < payloadFiles.Count)
            {
                WorkingFile payloadFile = payloadFiles[payloadIndex];
                ImmutableList<WorkingFile> profileFiles = [.. GetWorkingFiles(profilesRoot).Where(f => Path.GetExtension(f.FullPath) == ".profile" && f != payloadFile)];


                while (WriteFromProfile(payloadFile, profileFiles, profilesRoot, licensesRoot))
                { }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid entry");
                break;
            }
            break;
        case ConsoleKey.D4:
            DoWorkingDirChangeUI();
            break;
        case ConsoleKey.Q:
            return 0;
    }

    static bool WriteFromProfile(WorkingFile payloadFile, ImmutableList<WorkingFile> profileFiles, string profilesRoot, string licensesRoot)
    {
        int i = 0;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("Selected: ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(payloadFile.Leaf);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Select machine profile by number: ");
        foreach (WorkingFile filePath in profileFiles)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(i++);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($". {filePath.Leaf}");
        }
        if (int.TryParse(Console.ReadLine(), out int profileIndex) && profileIndex >= 0 && profileIndex < profileFiles.Count)
        {
            WorkingFile profileFile = profileFiles[profileIndex];
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Selected: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(profileFile.Leaf);
            var licResult = GenerateLicense(payloadFile.FullPath, profileFile.FullPath, profilesRoot, licensesRoot);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Result: {0}", licResult.Code);
            Console.WriteLine("{0}", licResult.Details);
            return true;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Invalid entry");
            return false;
        }
    }
}