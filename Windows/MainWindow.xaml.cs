﻿using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace AemulusModManager
{
    public partial class MainWindow : Window
    {
        public AemulusConfig config;
        public ConfigP3F p3fConfig;
        public ConfigP4G p4gConfig;
        public ConfigP5 p5Config;
        public Packages packages;
        public string game;
        private XmlSerializer xs;
        private XmlSerializer xp;
        private XmlSerializer xsp;
        private XmlSerializer xsm;
        public string modPath;
        private ObservableCollection<Package> PackageList;
        private ObservableCollection<DisplayedMetadata> DisplayedPackages;
        public bool emptySND;
        public bool useCpk;
        public bool messageBox;
        public bool fromMain;
        public bool bottomUpPriority;
        public string gamePath;
        public string launcherPath;
        public string elfPath;
        public string cpkLang;
        private BitmapImage bitmap;
        public List<FontAwesome5.ImageAwesome> buttons;

        public DisplayedMetadata InitDisplayedMetadata(Metadata m)
        {
            DisplayedMetadata dm = new DisplayedMetadata();
            dm.name = m.name;
            dm.id = m.id;
            dm.author = m.author;
            Version v;
            if (Version.TryParse(m.version, out v))
                dm.version = m.version;
            dm.description = m.description;
            dm.link = m.link;
            return dm;
        }

        private void OnChecked(object sender, RoutedEventArgs e)
        {
            var checkBox = e.OriginalSource as CheckBox;

            DisplayedMetadata package = checkBox?.DataContext as DisplayedMetadata;

            if (package != null)
            {
                package.enabled = true;
                foreach (var p in PackageList.ToList())
                {
                    if (p.path == package.path)
                        p.enabled = true;
                }
                updatePackages();
            }
        }

        // Events for Enabled checkboxes
        private void OnUnchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = e.OriginalSource as CheckBox;

            DisplayedMetadata package = checkBox?.DataContext as DisplayedMetadata;

            if (package != null)
            {
                package.enabled = false;
                foreach (var p in PackageList.ToList())
                {
                    if (p.path == package.path)
                        p.enabled = false;
                }
                updatePackages();
            }
        }

        // Hyperlink click event
        private void OnHyperlinkClick(object sender, RoutedEventArgs e)
        {
            var destination = ((Hyperlink)e.OriginalSource).NavigateUri;

            if (destination != null)
            {
                try
                {
                    Process.Start(destination.ToString());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Invalid Mod Page link. Perhaps missing \'www\' ({ex.Message})");
                }

            }
        }

        private StreamWriter sw;
        private TextBoxOutputter outputter;


        void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
        {
            string text = (string)e.Value;
            this.Dispatcher.Invoke(() =>
            {
                if (text.StartsWith("[INFO]"))
                    ConsoleOutput.AppendText($"{DateTime.Now} {text}\n", "#046300");
                else if (text.StartsWith("[WARNING]"))
                    ConsoleOutput.AppendText($"{DateTime.Now} {text}\n", "#764E00");
                else if (text.StartsWith("[ERROR]"))
                    ConsoleOutput.AppendText($"{DateTime.Now} {text}\n", "#AE1300");
                else
                    ConsoleOutput.AppendText($"{text}\n", "Black");
            });
        }

        void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
        {
            this.Dispatcher.Invoke(() =>
            {
                ConsoleOutput.AppendText(e.Value);
            });
        }

        // Autoscrolls to end whenever console updates
        private void ScrollToBottom(object sender, TextChangedEventArgs args)
        {
            ConsoleOutput.ScrollToEnd();
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            sw = new StreamWriter("AemulusLog.txt", false, Encoding.UTF8, 4096);
            outputter = new TextBoxOutputter(sw);
            packages = new Packages();

            outputter.WriteEvent += consoleWriter_WriteEvent;
            outputter.WriteLineEvent += consoleWriter_WriteLineEvent;
            Console.SetOut(outputter);

            Console.WriteLine($"[INFO] Launched Aemulus v2.4.0!");

            Directory.CreateDirectory($@"Packages");
            Directory.CreateDirectory($@"Original");
            Directory.CreateDirectory("Config");

            // Transfer all current packages to Persona 4 Golden folder
            if (!Directory.Exists($@"Packages\Persona 4 Golden") && !Directory.Exists($@"Packages\Persona 3 FES") && !Directory.Exists($@"Packages\Persona 5")
                && Directory.Exists("Packages") && Directory.GetDirectories("Packages").Any())
            {
                Console.WriteLine("[INFO] Transferring current packages to Persona 4 Golden subfolder...");
                MoveDirectory("Packages", "Persona 4 Golden");
                Directory.CreateDirectory("Packages");
                MoveDirectory("Persona 4 Golden", @"Packages\Persona 4 Golden");
            }


            string[] subdirs = Directory.GetDirectories("Original")
                            .Where(x => Path.GetFileName(x).StartsWith("data"))
                            .ToArray();
            Directory.CreateDirectory(@"Original\Persona 4 Golden");
            foreach (var d in subdirs)
                MoveDirectory(d, $@"Original\Persona 4 Golden\{Path.GetFileName(d)}");

            DisplayedPackages = new ObservableCollection<DisplayedMetadata>();
            PackageList = new ObservableCollection<Package>();

            // Retrieve initial thumbnail from embedded resource
            Assembly asm = Assembly.GetExecutingAssembly();
            Stream iconStream = asm.GetManifestResourceStream("AemulusModManager.Assets.Preview.png");
            bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = iconStream;
            bitmap.EndInit();
            Preview.Source = bitmap;

            
            // Initialize config
            config = new AemulusConfig();
            p5Config = new ConfigP5();
            p4gConfig = new ConfigP4G();
            p3fConfig = new ConfigP3F();
            config.p4gConfig = p4gConfig;
            config.p3fConfig = p3fConfig;
            config.p5Config = p5Config;

            // Initialize xml serializers
            XmlSerializer oldConfigSerializer = new XmlSerializer(typeof(Config));
            xs = new XmlSerializer(typeof(AemulusConfig));
            xp = new XmlSerializer(typeof(Packages));
            xsp = new XmlSerializer(typeof(Metadata));
            xsm = new XmlSerializer(typeof(ModXmlMetadata));

            buttons = new List<FontAwesome5.ImageAwesome>();
            buttons.Add(NewButton);
            buttons.Add(SwapButton);
            buttons.Add(FolderButton);
            buttons.Add(MergeButton);
            buttons.Add(ConfigButton);
            buttons.Add(LaunchButton);
            buttons.Add(RefreshButton);

            //Console.WriteLine($"[INFO] Initializing packages from {game.Replace(" ", "")}Config.xml");
            // Load in Config if it exists

            string file = @"Config\Config.xml";
            if (File.Exists(@"Config\Config.xml") || File.Exists(@"Config.xml"))
            {
                try
                {
                    if (File.Exists(@"Config.xml"))
                        file = @"Config.xml";
                    using (FileStream streamWriter = File.Open(file, FileMode.Open))
                    {
                        // Call the Deserialize method and cast to the object type.
                        
                        if (file == @"Config.xml")
                        {
                            Config oldConfig = (Config)oldConfigSerializer.Deserialize(streamWriter);
                            p4gConfig.reloadedPath = oldConfig.reloadedPath;
                            p4gConfig.exePath = oldConfig.exePath;
                            p4gConfig.modDir = oldConfig.modDir;
                            p4gConfig.emptySND = oldConfig.emptySND;
                            p4gConfig.cpkLang = oldConfig.cpkLang;
                            p4gConfig.useCpk = oldConfig.useCpk;

                            config.p4gConfig = p4gConfig;
                        }
                        else
                            config = (AemulusConfig)xs.Deserialize(streamWriter);
                        game = config.game;
                        if (game != "Persona 4 Golden" && game != "Persona 3 FES" && game != "Persona 5")
                        {
                            game = "Persona 4 Golden";
                            config.game = "Persona 4 Golden";
                        }

                        bottomUpPriority = config.bottomUpPriority;
                        
                        if (config.p3fConfig != null)
                            p3fConfig = config.p3fConfig;
                        if (config.p4gConfig != null)
                            p4gConfig = config.p4gConfig;
                        if (config.p5Config != null)
                            p5Config = config.p5Config;

                        if (game == "Persona 4 Golden")
                        {
                            // Default
                            if (cpkLang == null)
                            {
                                cpkLang = "data_e.cpk";
                                config.p4gConfig.cpkLang = "data_e.cpk";
                            }
                            modPath = config.p4gConfig.modDir;
                            gamePath = config.p4gConfig.exePath;
                            launcherPath = config.p4gConfig.reloadedPath;
                            emptySND = config.p4gConfig.emptySND;
                            cpkLang = config.p4gConfig.cpkLang;
                            useCpk = config.p4gConfig.useCpk;
                            messageBox = config.p4gConfig.disableMessageBox;
                            foreach (var button in buttons)
                                button.Foreground = new SolidColorBrush(Color.FromRgb(0xfe, 0xed, 0x2b));
                        }
                        else if (game == "Persona 3 FES")
                        {
                            modPath = config.p3fConfig.modDir;
                            gamePath = config.p3fConfig.isoPath;
                            elfPath = config.p3fConfig.elfPath;
                            launcherPath = config.p3fConfig.launcherPath;
                            messageBox = config.p3fConfig.disableMessageBox;
                            useCpk = false;
                            foreach (var button in buttons)
                                button.Foreground = new SolidColorBrush(Color.FromRgb(0x4f, 0xa4, 0xff));
                        }
                        else if (game == "Persona 5")
                        {
                            modPath = config.p5Config.modDir;
                            gamePath = config.p5Config.gamePath;
                            launcherPath = config.p5Config.launcherPath;
                            messageBox = config.p5Config.disableMessageBox;
                            useCpk = false;
                            foreach (var button in buttons)
                                button.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x00));
                        }
                    }
                    if (file == @"Config.xml")
                        File.Delete(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Invalid Config.xml ({ex.Message})");
                }

                switch (game)
                {
                    case "Persona 3 FES":
                        GameBox.SelectedIndex = 0;
                        break;
                    case "Persona 4 Golden":
                        GameBox.SelectedIndex = 1;
                        break;
                    case "Persona 5":
                        GameBox.SelectedIndex = 2;
                        break;
                }

                if (File.Exists($@"Config\{game.Replace(" ", "")}Packages.xml"))
                {
                    try
                    {
                        using (FileStream streamWriter = File.Open($@"Config\{game.Replace(" ", "")}Packages.xml", FileMode.Open))
                        {
                            // Call the Deserialize method and cast to the object type.
                            packages = (Packages)xp.Deserialize(streamWriter);
                            PackageList = packages.packages;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Invalid Packages.xml ({ex.Message})");
                    }
                }

                
                if (!Directory.Exists($@"Packages\{game}"))
                {
                    Console.WriteLine($@"[INFO] Creating Packages\{game}");
                    Directory.CreateDirectory($@"Packages\{game}");
                }
                
                // Create displayed metadata from packages in PackageList and their respective Package.xml's
                foreach (var package in PackageList)
                {
                    string xml = $@"Packages\{game}\{package.path}\Package.xml";
                    Metadata m;
                    DisplayedMetadata dm = new DisplayedMetadata();
                    if (File.Exists(xml))
                    {
                        m = new Metadata();
                        try
                        {
                            using (FileStream streamWriter = File.Open(xml, FileMode.Open))
                            {
                                try
                                {
                                    m = (Metadata)xsp.Deserialize(streamWriter);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message}) Fix or delete the current Package.xml then refresh to use.");
                                    continue;
                                }
                                dm.name = m.name;
                                dm.id = m.id;
                                dm.author = m.author;
                                dm.version = m.version;
                                dm.link = m.link;
                                dm.description = m.description;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message}) Fix or delete the current Package.xml then refresh to use.");
                            continue;
                        }
                    }

                    dm.path = package.path;
                    dm.enabled = package.enabled;
                    DisplayedPackages.Add(dm);
                }
                ModGrid.ItemsSource = DisplayedPackages;
                
            }
            else // No config found
            {
                game = "Persona 4 Golden";
                config.game = "Persona 4 Golden";
                cpkLang = "data_e.cpk";
                config.p4gConfig.cpkLang = "data_e.cpk";
                foreach (var button in buttons)
                    button.Foreground = new SolidColorBrush(Color.FromRgb(0xfe, 0xed, 0x2b));
            }



            if (game == "Persona 4 Golden" && config.p4gConfig.modDir != "" && config.p4gConfig.modDir != null)
                modPath = config.p4gConfig.modDir;
            else if (game == "Persona 3 FES" && config.p3fConfig.modDir != "" && config.p3fConfig.modDir != null)
                modPath = config.p3fConfig.modDir;
            else if (game == "Persona 5" && config.p5Config.modDir != "" && config.p5Config.modDir != null)
                modPath = config.p5Config.modDir;

            if (modPath == "" || modPath == null)
            {
                MergeButton.IsHitTestVisible = false;
                MergeButton.Foreground = new SolidColorBrush(Colors.Gray);
            }
            // Create Packages directory if it doesn't exist
            Directory.CreateDirectory("Packages");
            Directory.CreateDirectory(@"Packages\Persona 3 FES");
            Directory.CreateDirectory(@"Packages\Persona 4 Golden");
            Directory.CreateDirectory(@"Packages\Persona 5");
            Directory.CreateDirectory("Original");

            Refresh();
            updateConfig();
            updatePackages();

            Description.Document = ConvertToFlowDocument("Aemulus means \"Rival\" in Latin. It was chosen since it " +
                "was made to rival Mod Compendium.\n\n(You are seeing this message because no package is selected or " +
                "the package has no description.)");

            if (!bottomUpPriority)
            {
                TopPriority.Text = "Higher Priority";
            }
            else
            {
                TopPriority.Text = "Lower Priority";
            }

            LaunchButton.ToolTip = $"Launch {game}";
        }

        public Task pacUnpack(string directory)
        {
            return Task.Run(() =>
            {
                if (game == "Persona 4 Golden")
                    PacUnpacker.Unpack(directory, cpkLang);
                else if (game == "Persona 3 FES")
                    PacUnpacker.Unzip(directory);
                else if (game == "Persona 5")
                    PacUnpacker.UnpackCPK(directory);
                
                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    foreach (var button in buttons)
                    {
                        button.IsHitTestVisible = true;
                        if (game == "Persona 3 FES")
                            button.Foreground = new SolidColorBrush(Color.FromRgb(0x4f, 0xa4, 0xff));
                        else if (game == "Persona 4 Golden")
                            button.Foreground = new SolidColorBrush(Color.FromRgb(0xfe, 0xed, 0x2b));
                        else
                            button.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x00));
                    }
                    ModGrid.IsHitTestVisible = true;
                    GameBox.IsHitTestVisible = true;
                    if (!fromMain && !messageBox)
                    {
                        NotificationBox notification = new NotificationBox("Finished Unpacking!");
                        notification.ShowDialog();
                        Activate();
                    }
                });
                if ((game == "Persona 4 Golden" && !Directory.Exists($@"Original\{game}\{Path.GetFileNameWithoutExtension(cpkLang)}"))
                    || (game == "Persona 3 FES" && !Directory.Exists($@"Original\{game}\DATA")
                    && !Directory.Exists($@"Original\{game}\BTL"))
                    || (game == "Persona 5" && !Directory.Exists($@"Original\{game}")))
                    Console.WriteLine($@"[ERROR] Failed to unpack everything from {game}! Please check if you have all prerequisites installed!");
            });
        }

        private void LaunchClick(object sender, RoutedEventArgs e)
        {
            if ((gamePath != "" && gamePath != null && launcherPath != "" && launcherPath != null)
                || (elfPath != "" && elfPath != null && launcherPath != "" && launcherPath != null))
            {
                if (game != "Persona 3 FES")
                    Console.WriteLine($"[INFO] Launching {gamePath} with {launcherPath}");
                else
                    Console.WriteLine($"[INFO] Launching {elfPath} with {launcherPath}");
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.FileName = launcherPath;
                if (!File.Exists(launcherPath))
                {
                    Console.WriteLine($"[ERROR] Couldn't find {launcherPath}. Please correct the file path in config.");
                    return;
                }
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                if (game == "Persona 4 Golden")
                {
                    if (!File.Exists(gamePath))
                    {
                        Console.WriteLine($"[ERROR] Couldn't find {gamePath}. Please correct the file path in config.");
                        return;
                    }
                    startInfo.Arguments = $"--launch \"{gamePath}\"";
                }
                else if (game == "Persona 3 FES")
                {
                    string tempElfPath = null, tempGamePath = null;

                    if (p3fConfig.advancedLaunchOptions)
                    {
                        NotificationBox notification = new NotificationBox("Would you like to choose a custom ELF/SLUS to launch with? To use the executable included in the ISO, choose \"No\".", false);
                        notification.ShowDialog();
                        Activate();
                        if (notification.YesNo)
                        {
                            CommonOpenFileDialog tempElfDialog = new CommonOpenFileDialog();
                            tempElfDialog.InitialDirectory = new FileInfo(elfPath).DirectoryName;
                            if (tempElfDialog.ShowDialog() == CommonFileDialogResult.Ok)
                            {
                                tempElfPath = tempElfDialog.FileName;
                            }
                        }
                        notification = new NotificationBox("Would you like to choose a custom ISO to launch with? If you're using HostFS, choose \"No\".", false);
                        notification.ShowDialog();
                        Activate();
                        if (notification.YesNo)
                        {
                            CommonOpenFileDialog tempGameDialog = new CommonOpenFileDialog();
                            tempGameDialog.InitialDirectory = new FileInfo(gamePath).DirectoryName;
                            if (tempGameDialog.ShowDialog() == CommonFileDialogResult.Ok)
                            {
                                tempGamePath = tempGameDialog.FileName;
                            }
                        }

                        // If the user said "No" to both options, we'll fall back to the old behavior
                        // of just launching the ELF selected in the config.
                        if (tempElfPath == null && tempGamePath == null)
                        {
                            tempElfPath = elfPath;
                        }
                    }
                    else
                    {
                        // If the user doesn't want to be prompted for extra options,
                        // just automatically launch the ELF selected in the config.
                        tempElfPath = elfPath;
                    }

                    // Build the PCSX2 launch arguments based on what we've chosen/what's non-null
                    startInfo.Arguments = "--nogui";
                    if (tempElfPath != null)
                    {
                        if (!File.Exists(tempElfPath))
                        {
                            Console.WriteLine($"[ERROR] Couldn't find {tempElfPath}. Please correct the file path in config.");
                            return;
                        }
                        startInfo.Arguments += $" --elf=\"{tempElfPath}\"";
                    }
                    if (tempGamePath != null)
                    {
                        if (!File.Exists(tempGamePath))
                        {
                            Console.WriteLine($"[ERROR] Couldn't find {tempGamePath}. Please correct the file path in config.");
                            return;
                        }
                        startInfo.Arguments += $" \"{tempGamePath}\"";
                    }
                }
                else if (game == "Persona 5")
                {
                    if (!File.Exists(gamePath))
                    {
                        Console.WriteLine($"[ERROR] Couldn't find {gamePath}. Please correct the file path in config.");
                        return;
                    }
                    Console.WriteLine($"[INFO] If the game is lagging set the global config to your special config for Persona 5.");
                    startInfo.Arguments = $"--no-gui \"{gamePath}\"";
                }

                foreach (var button in buttons)
                {
                    button.IsHitTestVisible = false;
                    button.Foreground = new SolidColorBrush(Colors.Gray);
                }
                GameBox.IsHitTestVisible = false;
                ModGrid.IsHitTestVisible = false;

                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    //process.WaitForExit(); // Freezes aemulus
                }

                foreach (var button in buttons)
                {
                    button.IsHitTestVisible = true;
                    if (game == "Persona 3 FES")
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0x4f, 0xa4, 0xff));
                    else if (game == "Persona 4 Golden")
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0xfe, 0xed, 0x2b));
                    else
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x00));
                }
                ModGrid.IsHitTestVisible = true;
                GameBox.IsHitTestVisible = true;
            }
            else
                Console.WriteLine("[ERROR] Please setup shortcut in config menu.");
        }

        private void ConfigWdwClick(object sender, RoutedEventArgs e)
        {

            if (game == "Persona 4 Golden")
            {
                ConfigWindowP4G cWindow = new ConfigWindowP4G(this) { Owner = this };
                cWindow.DataContext = this;
                cWindow.ShowDialog();
            }
            else if (game == "Persona 3 FES")
            {
                ConfigWindowP3F cWindow = new ConfigWindowP3F(this) { Owner = this };
                cWindow.DataContext = this;
                cWindow.ShowDialog();
            }
            else if (game == "Persona 5")
            {
                ConfigWindowP5 cWindow = new ConfigWindowP5(this) { Owner = this };
                cWindow.DataContext = this;
                cWindow.ShowDialog();
            }
        }

        private void UpdateMetadata()
        {
            // Update metadata
            List<DisplayedMetadata> temp = DisplayedPackages.ToList();
            foreach (var package in temp)
            {
                if (File.Exists($@"Packages\{game}\{package.path}\Package.xml"))
                {
                    try
                    {
                        using (FileStream streamWriter = File.Open($@"Packages\{game}\{package.path}\Package.xml", FileMode.Open))
                        {
                            Metadata metadata = null;
                            try
                            {
                                metadata = (Metadata)xsp.Deserialize(streamWriter);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message}) Fix or delete the current Package.xml then refresh to use.");
                                continue;
                            }
                            package.name = metadata.name;
                            package.id = metadata.id;
                            package.author = metadata.author;
                            package.version = metadata.version;
                            package.link = metadata.link;
                            package.description = metadata.description;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message}) Fix or delete the current Package.xml then refresh to use.");
                        continue;
                    }
                }
            }
            DisplayedPackages = new ObservableCollection<DisplayedMetadata>(temp);
        }

        public static void MoveDirectory(string source, string target)
        {
            var sourcePath = source.TrimEnd('\\', ' ');
            var targetPath = target.TrimEnd('\\', ' ');
            var files = Directory.GetFiles(sourcePath, "*", System.IO.SearchOption.AllDirectories)
                                 .GroupBy(s => Path.GetDirectoryName(s));
            foreach (var folder in files)
            {
                var targetFolder = folder.Key.Replace(sourcePath, targetPath);
                Directory.CreateDirectory(targetFolder);
                foreach (var file in folder)
                {
                    var targetFile = Path.Combine(targetFolder, Path.GetFileName(file));
                    if (File.Exists(targetFile)) File.Delete(targetFile);
                    File.Move(file, targetFile);
                }
            }
            Directory.Delete(source, true);
        }

        // Refresh both PackageList and DisplayedPackages
        private void Refresh()
        {
            Metadata metadata;
            // First remove all deleted packages and update package id's to match metadata
            foreach (var package in PackageList.ToList())
            {
                if (!Directory.Exists($@"Packages\{game}\{package.path}"))
                {
                    PackageList.Remove(package);
                    List<DisplayedMetadata> temp = DisplayedPackages.ToList();
                    temp.RemoveAll(x => x.path == package.path);
                    DisplayedPackages = new ObservableCollection<DisplayedMetadata>(temp);
                }
                if (File.Exists($@"Packages\{game}\{package.path}\Package.xml"))
                {
                    try
                    {
                        using (FileStream streamWriter = File.Open($@"Packages\{game}\{package.path}\Package.xml", FileMode.Open))
                        {
                            try
                            {
                                metadata = (Metadata)xsp.Deserialize(streamWriter);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message}) Fix or delete the current Package.xml then refresh to use.");
                                continue;
                            }
                            package.id = metadata.id;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path} ({ex.Message}) Fix or delete the current Package.xml then refresh to use.");
                        continue;
                    }
                }
            }

            UpdateMetadata();

            // Get all packages from Packages folder (Adding packages)
            foreach (var package in Directory.GetDirectories($@"Packages\{game}"))
            {
                if (File.Exists($@"{package}\Package.xml"))
                {
                    using (FileStream streamWriter = File.Open($@"{package}\Package.xml", FileMode.Open))
                    {
                        try
                        {
                            metadata = (Metadata)xsp.Deserialize(streamWriter);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Invalid Package.xml for {package} ({ex.Message}) Fix or delete the current Package.xml then refresh to use.");
                            continue;
                        }
                        // Add package to list if it doesn't exist
                        if (!PackageList.ToList().Any(x => x.path == Path.GetFileName(package))
                            && !DisplayedPackages.ToList().Any(x => x.path == Path.GetFileName(package)))
                        {
                            // Add new package to both collections
                            DisplayedMetadata dm = InitDisplayedMetadata(metadata);
                            Package p = new Package();
                            p.enabled = false;
                            p.id = metadata.id;
                            p.path = Path.GetFileName(package);
                            PackageList.Add(p);
                            dm.enabled = false;
                            dm.path = Path.GetFileName(package);
                            DisplayedPackages.Add(dm);
                        }
                    }
                }
                // Create Package.xml
                else
                {
                    Console.WriteLine($"[WARNING] No Package.xml found for {Path.GetFileName(package)}, creating a simple one...");
                    // Create metadata
                    Metadata newMetadata = new Metadata();
                    newMetadata.name = Path.GetFileName(package);
                    newMetadata.id = newMetadata.name.Replace(" ", "").ToLower();

                    
                    List<string> dirFiles = Directory.GetFiles(package).ToList();
                    List<string> dirFolders = Directory.GetDirectories(package, "*", System.IO.SearchOption.TopDirectoryOnly).ToList();
                    dirFiles = dirFiles.Concat(dirFolders).ToList();
                    if (File.Exists($@"{package}\Mod.xml") && Directory.Exists($@"{package}\Data"))
                    {
                        //If mod folder contains Data folder and mod.xml, import mod compendium mod.xml...
                        string modXml = $@"{package}\Mod.xml";
                        using (FileStream streamWriter = File.Open(modXml, FileMode.Open))
                        {
                            //Deserialize Mod.xml & Use metadata
                            ModXmlMetadata m = null;
                            try
                            {
                                m = (ModXmlMetadata)xsm.Deserialize(streamWriter);
                                newMetadata.id = m.Author.ToLower().Replace(" ", "") + "." + m.Title.ToLower().Replace(" ", "");
                                newMetadata.author = m.Author;
                                newMetadata.version = m.Version;
                                newMetadata.link = m.Url;
                                newMetadata.description = m.Description;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Invalid Mod.xml for {package} ({ex.Message})");
                                continue;
                            }
                        }
                        //Move files out of Data folder
                        string dataDir = $@"{package}\Data";
                        if (Directory.Exists(dataDir))
                        {
                            MoveDirectory(dataDir, $@"temp");
                            MoveDirectory($@"temp", package);
                        }

                        if (Directory.Exists("temp"))
                        {
                            try
                            {
                                setAttributesNormal(new DirectoryInfo("temp"));
                                DeleteDirectory("temp");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($@"[ERROR] Couldn't delete temp ({ex.Message})");
                            }
                        }
                        //Make sure Data folder is gone
                        if (Directory.Exists(dataDir) && !Directory.EnumerateFileSystemEntries(dataDir).Any())
                            DeleteDirectory(dataDir);
                        //Goodbye old friend
                        File.Delete(modXml);
                    }
                    else
                    {
                        newMetadata.author = "";
                        newMetadata.version = "";
                        newMetadata.link = "";
                        newMetadata.description = "";
                    }
                    using (FileStream streamWriter = File.Create($@"{package}\Package.xml"))
                    {
                        try
                        {
                            xsp.Serialize(streamWriter, newMetadata);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Invalid Package.xml for {package} ({ex.Message}) Fix or delete the current Package.xml then refresh to use.");
                        }
                    }
                    if (!PackageList.ToList().Any(x => x.path == Path.GetFileName(package))
                            && !DisplayedPackages.ToList().Any(x => x.path == Path.GetFileName(package)))
                    {
                        // Create package
                        Package newPackage = new Package();
                        newPackage.enabled = false;
                        newPackage.path = Path.GetFileName(package);
                        newPackage.id = newMetadata.id;
                        PackageList.Add(newPackage);
                        // Create displayedmetadata
                        DisplayedMetadata newDisplayedMetadata = InitDisplayedMetadata(newMetadata);
                        newDisplayedMetadata.enabled = false;
                        newDisplayedMetadata.path = newPackage.path;
                        DisplayedPackages.Add(newDisplayedMetadata);
                    }
                    else
                    {
                        UpdateMetadata();
                    }
                }
            }

            // Update DisplayedPackages
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                ModGrid.ItemsSource = DisplayedPackages;
                // Trigger select event to refresh description and Preview.png
                ModGrid.SetSelectedItem(ModGrid.GetSelectedItem());
            });
            Console.WriteLine($"[INFO] Refreshed!");
        }

        private void RefreshClick(object sender, RoutedEventArgs e)
        {
            Refresh();
            updateConfig();
            updatePackages();
        }

        private void NewClick(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[INFO] Creating new package!");
            CreatePackage newPackage = new CreatePackage(null);
            newPackage.ShowDialog();
            if (newPackage.metadata != null)
            {
                string path;
                if (newPackage.metadata.version != "" && newPackage.metadata.version.Length > 0)
                    path = $@"Packages\{game}\{newPackage.metadata.name} {newPackage.metadata.version}";
                else
                    path = $@"Packages\{game}\{newPackage.metadata.name}";
                if (!Directory.Exists(path))
                {
                    try
                    {
                        Directory.CreateDirectory(path);
                        using (FileStream streamWriter = File.Create($@"{path}\Package.xml"))
                        {
                            try
                            {
                                xsp.Serialize(streamWriter, newPackage.metadata);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Couldn't create directory/Package.xml. ({ex.Message})");
                            }
                        }
                        if (File.Exists(newPackage.thumbnailPath))
                        {
                            string extension = Path.GetExtension(newPackage.thumbnailPath).ToLower();
                            if (extension == ".png" || extension == ".jpg")
                                File.Copy(newPackage.thumbnailPath, $@"{path}\Preview{extension}", true);
                        }
                        Refresh();
                        updateConfig();
                        updatePackages();
                        ProcessStartInfo StartInformation = new ProcessStartInfo();
                        StartInformation.FileName = path;
                        Process process = Process.Start(StartInformation);
                        Console.WriteLine("[INFO] Opened new package folder.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Couldn't create directory/Package.xml. ({ex.Message})");
                    }
                }
                else
                {
                    Console.WriteLine($"[ERROR] {newPackage.metadata.name} already exists, not creating new package.");
                }
            }
        }

        private string selectExe(string title, string extension)
        {
            string type = "Application";
            if (extension == ".iso")
                type = "PS2 Disc";
            else if (extension == ".bin")
                type = "EBOOT";
            var openExe = new CommonOpenFileDialog();
            openExe.Filters.Add(new CommonFileDialogFilter(type, $"*{extension}"));
            openExe.EnsurePathExists = true;
            openExe.EnsureValidNames = true;
            openExe.Title = title;
            if (openExe.ShowDialog() == CommonFileDialogResult.Ok)
            {
                return openExe.FileName;
            }
            return null;
        }

        private async void MergeClick(object sender, RoutedEventArgs e)
        {
            if ((game == "Persona 4 Golden" && !Directory.Exists($@"Original\{game}\{Path.GetFileNameWithoutExtension(cpkLang)}"))
                    || (game == "Persona 3 FES" && !Directory.Exists($@"Original\{game}\DATA")
                    && !Directory.Exists($@"Original\{game}\BTL"))
                    || (game == "Persona 5" && !Directory.Exists($@"Original\{game}")))
            { 
                Console.WriteLine("[WARNING] Aemulus can't find your Base files in the Original folder.");
                Console.WriteLine($"[WARNING] Attempting to unpack base files first.");

                if (gamePath == "" || gamePath == null)
                {
                    string selectedPath;
                    if (game == "Persona 4 Golden")
                    {
                        selectedPath = selectExe("Select P4G.exe to unpack", ".exe");
                        if (selectedPath != null && Path.GetFileName(selectedPath) == "P4G.exe")
                        {
                            gamePath = selectedPath;
                            config.p4gConfig.exePath = gamePath;
                            updateConfig();
                        }
                        else
                            Console.WriteLine("[ERROR] Incorrect file chosen.");
                    }
                    else if (game == "Persona 3 FES")
                    {
                        selectedPath = selectExe("Select P3F's iso to unpack", ".iso");
                        if (selectedPath != null)
                        {
                            gamePath = selectedPath;
                            config.p3fConfig.isoPath = gamePath;
                            updateConfig();
                        }
                        else
                            Console.WriteLine("[ERROR] Incorrect file chosen.");
                    }
                    else if (game == "Persona 5")
                    {
                        selectedPath = selectExe("Select P5's EBOOT.BIN to unpack", ".bin");
                        if (selectedPath != null && Path.GetFileName(selectedPath) == "EBOOT.BIN")
                        {
                            gamePath = selectedPath;
                            config.p5Config.gamePath = gamePath;
                            updateConfig();
                        }
                        else
                            Console.WriteLine("[ERROR] Incorrect file chosen.");
                    }
                }

                if (gamePath == "" || gamePath == null)
                    return;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                });

                foreach (var button in buttons)
                {
                    button.IsHitTestVisible = false;
                    button.Foreground = new SolidColorBrush(Colors.Gray);
                }
                GameBox.IsHitTestVisible = false;
                ModGrid.IsHitTestVisible = false;

                fromMain = true;

                if (game == "Persona 3 FES")
                    await pacUnpack(gamePath);
                else
                    await pacUnpack(Path.GetDirectoryName(gamePath));
                fromMain = false;

                if ((game == "Persona 4 Golden" && !Directory.Exists($@"Original\{game}\{Path.GetFileNameWithoutExtension(cpkLang)}"))
                    || (game == "Persona 3 FES" && !Directory.Exists($@"Original\{game}\DATA")
                    && !Directory.Exists($@"Original\{game}\BTL"))
                    || (game == "Persona 5" && !Directory.Exists($@"Original\{game}")))
                {
                    Console.WriteLine($@"[ERROR] Failed to unpack everything from {game}! Please check if you have all prerequisites installed!");
                    return;
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });

            foreach (var button in buttons)
            {
                button.IsHitTestVisible = false;
                button.Foreground = new SolidColorBrush(Colors.Gray);
            }
            GameBox.IsHitTestVisible = false;
            ModGrid.IsHitTestVisible = false;

            await unpackThenMerge();

            ModGrid.IsHitTestVisible = true;
            foreach (var button in buttons)
            {
                button.IsHitTestVisible = true;
                if (game == "Persona 3 FES")
                    button.Foreground = new SolidColorBrush(Color.FromRgb(0x4f, 0xa4, 0xff));
                else if (game == "Persona 4 Golden")
                    button.Foreground = new SolidColorBrush(Color.FromRgb(0xfe, 0xed, 0x2b));
                else
                    button.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x00));
            }
            GameBox.IsHitTestVisible = true;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = null;
            });
        }

        private async Task unpackThenMerge()
        {
            await Task.Run(() =>
            {
                Refresh();
                if (!Directory.Exists(modPath))
                {
                    Console.WriteLine("[ERROR] Current output folder doesn't exist! Please select it again.");
                    return;
                }
                List<string> packages = new List<string>();
                foreach (Package m in PackageList)
                {
                    if (m.enabled)
                    {
                        packages.Add($@"Packages\{game}\{m.path}");
                        Console.WriteLine($@"[INFO] Using {m.path} in loadout");
                        if (game == "Persona 4 Golden" && (Directory.Exists($@"Packages\{game}\{m.path}\{Path.GetFileNameWithoutExtension(cpkLang)}")
                            || Directory.Exists($@"Packages\{game}\{m.path}\movie") || Directory.Exists($@"Packages\{game}\{m.path}\preappfile")) && !useCpk)
                        {
                            Console.WriteLine($"[WARNING] {m.path} is using CPK folder paths, setting Use CPK Structure to true");
                            useCpk = true;
                        }
                    }
                }
                if (!bottomUpPriority)
                    packages.Reverse();
                if (packages.Count == 0)
                {
                    Console.WriteLine("[WARNING] No packages enabled in loadout, emptying output folder...");
                    string path = modPath;
                    if (game == "Persona 5")
                    {
                        path = $@"{modPath}\mod";
                        Directory.CreateDirectory(path);
                    }

                    if (!Directory.EnumerateFileSystemEntries(path).Any())
                    {
                        Console.WriteLine($"[INFO] Output folder already empty");
                        return;
                    }


                    if (!messageBox)
                    {
                        bool YesNo = false;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Mouse.OverrideCursor = null;
                            NotificationBox notification = new NotificationBox($"Confirm DELETING THE ENTIRE CONTENTS of {path}?", false);
                            notification.ShowDialog();
                            YesNo = notification.YesNo;
                            Mouse.OverrideCursor = Cursors.Wait;
                        });
                        if (!YesNo)
                        {
                            Console.WriteLine($"[INFO] Cancelled emptying output folder");
                            return;
                        }
                    }

                    binMerge.Restart(path, emptySND, game, cpkLang);
                    Console.WriteLine("[INFO] Finished emptying output folder!");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Mouse.OverrideCursor = null;
                        if (!messageBox)
                        {
                            NotificationBox notification = new NotificationBox("Finished emptying output folder!");
                            notification.ShowDialog();
                            Activate();
                        }
                    });
                    return;
                }
                else
                {
                    string path = modPath;
                    if (game == "Persona 5")
                    {
                        path = $@"{modPath}\mod";
                        Directory.CreateDirectory(path);
                    }

                    if (!messageBox && Directory.EnumerateFileSystemEntries(path).Any())
                    {
                        bool YesNo = false;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Mouse.OverrideCursor = null;
                            NotificationBox notification = new NotificationBox($"Confirm DELETING THE ENTIRE CONTENTS of {path} before building?", false);
                            notification.ShowDialog();
                            YesNo = notification.YesNo;
                            Mouse.OverrideCursor = Cursors.Wait;
                        });
                        if (!YesNo)
                        {
                            Console.WriteLine($"[INFO] Cancelled build");
                            return;
                        }
                    }

                    binMerge.Restart(path, emptySND, game, cpkLang);
                    binMerge.Unpack(packages, path, useCpk, cpkLang, game);
                    binMerge.Merge(path, game);

                    // Only run if tblpatches exists
                    if (packages.Exists(x => Directory.Exists($@"{x}\tblpatches")))
                    {
                        tblPatch.Patch(packages, path, useCpk, cpkLang, game);
                    }

                    // Only run if tblpatches exists
                    if (game == "Persona 4 Golden" && packages.Exists(x => Directory.Exists($@"{x}\preappfile")))
                    {
                        PreappfileAppend.Append(Path.GetDirectoryName(path), cpkLang);
                    }

                    if (game == "Persona 5")
                    {
                        binMerge.MakeCpk(path);
                        if (!File.Exists($@"{modPath}\mod.cpk"))
                            Console.WriteLine("[ERROR] Failed to build mod.cpk!");
                    }

                    if (game == "Persona 4 Golden" && File.Exists($@"{modPath}\patches\BGME_Base.patch") && File.Exists($@"{modPath}\patches\BGME_Main.patch"))
                        Console.WriteLine("[WARNING] BGME_Base.patch and BGME_Main.patch found in your patches folder which will result in no music in battles.");

                    Console.WriteLine("[INFO] Finished Building!");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Mouse.OverrideCursor = null;
                        if (!messageBox)
                        {
                            NotificationBox notification = new NotificationBox("Finished Building!");
                            notification.ShowDialog();
                            Activate();
                        }
                    });
                }
            });
        }

        public void updateConfig()
        {
            using (FileStream streamWriter = File.Create($@"Config\Config.xml"))
            {
                try
                {
                    xs.Serialize(streamWriter, config);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($@"[ERROR] Couldn't update Config\Config.xml ({ex.Message})");
                }
            }
        }

        public void updatePackages()
        {
            packages.packages = PackageList;
            using (FileStream streamWriter = File.Create($@"Config\{game.Replace(" ", "")}Packages.xml"))
            {
                try
                {
                    xp.Serialize(streamWriter, packages);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($@"[ERROR] Couldn't update Config\{game.Replace(" ", "")}Packages.xml ({ex.Message})");
                }
            }
        }

        private void rowSelected(object sender, SelectionChangedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            if (row != null)
            {
                // Set description
                if (row.description != null && row.description.Length > 0)
                {
                    Description.Document = ConvertToFlowDocument(row.description);
                }
                else
                {
                    Description.Document = ConvertToFlowDocument("Aemulus means \"Rival\" in Latin. It was chosen since it " +
                        "was made to rival Mod Compendium.\n\n(You are seeing this message because no package is selected or " +
                        "the package has no description.)");
                }

                // Set requirement visibility
                if (Directory.Exists($@"Packages\{game}\{row.path}\patches"))
                    Inaba.Visibility = Visibility.Visible;
                else
                    Inaba.Visibility = Visibility.Collapsed;
                if (File.Exists($@"Packages\{game}\{row.path}\SND\HeeHeeHo.uwus"))
                    HHH.Visibility = Visibility.Visible;
                else
                    HHH.Visibility = Visibility.Collapsed;
                if (Directory.Exists($@"Packages\{game}\{row.path}\patches") || File.Exists($@"Packages\{game}\{row.path}\SND\HeeHeeHo.uwus"))
                    Reqs.Visibility = Visibility.Visible;
                else
                    Reqs.Visibility = Visibility.Collapsed;

                // Enable/disable convert to 1.4.0
                ConvertCPK.IsEnabled = false;
                foreach (var folder in Directory.GetDirectories($@"Packages\{game}\{row.path}"))
                {
                    if (Path.GetFileName(folder).StartsWith("data0") || Path.GetFileName(folder).StartsWith("movie0"))
                    {
                        ConvertCPK.IsEnabled = true;
                    }
                }

                // Set image
                string path = $@"Packages\{game}\{row.path}";
                if (File.Exists($@"{path}\Preview.png") || File.Exists($@"{path}\Preview.jpg"))
                {
                    try
                    {
                        byte[] imageBytes = null;
                        if (File.Exists($@"{path}\Preview.png"))
                            imageBytes = File.ReadAllBytes($@"{path}\Preview.png");
                        else
                            imageBytes = File.ReadAllBytes($@"{path}\Preview.jpg");
                        var stream = new MemoryStream(imageBytes);
                        var img = new BitmapImage();

                        img.BeginInit();
                        img.StreamSource = stream;
                        img.EndInit();
                        Preview.Source = img;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] {ex.Message}");
                    }
                }
                else
                    Preview.Source = bitmap;

            }
        }

        // Update config order when rows are changed
        private void ModGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            DisplayedMetadata dm = (DisplayedMetadata)e.Row.Item;
            foreach (var p in PackageList.ToList())
            {
                if (dm.path == p.path)
                {
                    Package temp = p;
                    PackageList.Remove(p);
                    PackageList.Insert(DisplayedPackages.IndexOf(dm), temp);
                }
            }
            updateConfig();
            updatePackages();
        }

        private FlowDocument ConvertToFlowDocument(string text)
        {
            var flowDocument = new FlowDocument();

            var regex = new Regex(@"(https?:\/\/[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var matches = regex.Matches(text).Cast<Match>().Select(m => m.Value).ToList();

            var paragraph = new Paragraph();
            flowDocument.Blocks.Add(paragraph);

            foreach (var segment in regex.Split(text))
            {
                if (matches.Contains(segment))
                {
                    var hyperlink = new Hyperlink(new Run(segment))
                    {
                        NavigateUri = new Uri(segment),
                    };
                    hyperlink.RequestNavigate += (sender, args) => Process.Start(segment);

                    paragraph.Inlines.Add(hyperlink);
                }
                else
                {
                    paragraph.Inlines.Add(new Run(segment));
                }
            }

            return flowDocument;
        }

        private void Inaba_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://gamebanana.com/tools/6872");
        }

        private void HHH_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("https://gamebanana.com/gamefiles/12806");
        }

        private void OpenItem_Click(object sender, RoutedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            if (row != null)
            {
                if (Directory.Exists($@"Packages\{game}\{row.path}"))
                {
                    try
                    {
                        ProcessStartInfo StartInformation = new ProcessStartInfo();
                        StartInformation.FileName = $@"Packages\{game}\{row.path}";
                        Process process = Process.Start(StartInformation);
                        Console.WriteLine($@"[INFO] Opened Packages\{game}\{row.path}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($@"[ERROR] Couldn't open Packages\{game}\{row.path} ({ex.Message})");
                    }
                }
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            if (row != null)
            {
                NotificationBox notification = new NotificationBox($@"Are you sure you want to delete Packages\{row.path}?", false);
                notification.ShowDialog();
                Activate();
                if (Directory.Exists($@"Packages\{game}\{row.path}") && notification.YesNo)
                {
                    Console.WriteLine($@"[INFO] Deleted Packages\{game}\{row.path}.");
                    try
                    {
                        setAttributesNormal(new DirectoryInfo($@"Packages\{game}\{row.path}"));
                        DeleteDirectory($@"Packages\{game}\{row.path}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($@"[ERROR] Couldn't delete Packages\{game}\{row.path} ({ex.Message})");
                    }
                    Refresh();
                    updateConfig();
                    updatePackages();
                }
            }
        }

        private void EditItem_Click(object sender, RoutedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            if (row != null && File.Exists($@"Packages\{game}\{row.path}\Package.xml"))
            {
                Metadata m = new Metadata();
                m.name = row.name;
                m.author = row.author;
                m.id = row.id;
                m.version = row.version;
                m.link = row.link;
                m.description = row.description;
                CreatePackage createPackage = new CreatePackage(m);
                createPackage.ShowDialog();
                if (createPackage.metadata != null)
                {
                    try
                    {
                        using (FileStream streamWriter = File.Create($@"Packages\{game}\{row.path}\Package.xml"))
                        {
                            try
                            {
                                xsp.Serialize(streamWriter, createPackage.metadata);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($@"[ERROR] Couldn't serialize Packages\{game}\{row.path}\Package.xml ({ex.Message})");
                            }
                        }
                        if (File.Exists(createPackage.thumbnailPath))
                        {
                            string extension = Path.GetExtension(createPackage.thumbnailPath).ToLower();
                            if (extension == ".png" || extension == ".jpg")
                                File.Copy(createPackage.thumbnailPath, $@"Packages\{game}\{row.path}\Preview{extension}", true);
                        }

                        Refresh();
                        updateConfig();
                        updatePackages();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] {ex.Message}");
                    }
                }
            }
        }

        private async void ZipItem_Click(object sender, RoutedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            if (row != null && Directory.Exists($@"Packages\{game}\{row.path}"))
            {
                var openFolder = new System.Windows.Forms.SaveFileDialog();
                openFolder.FileName = $"{row.path}.7z";
                openFolder.Title = $"Select a file to zip to";
                openFolder.Filter = "7zip | *.7z";
                if (openFolder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    await ZipItem($@"Packages\{game}\{row.path}", openFolder.FileName);
                    ProcessStartInfo StartInformation = new ProcessStartInfo();
                    StartInformation.FileName = Path.GetDirectoryName(openFolder.FileName);
                    Process process = Process.Start(StartInformation);
                }
            }
        }

        private async Task ZipItem(string path, string output)
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.CreateNoWindow = true;
                startInfo.FileName = @"Dependencies\7z\7z.exe";
                if (!File.Exists(startInfo.FileName))
                {
                    Console.Write($"[ERROR] Couldn't find {startInfo.FileName}. Please check if it was blocked by your anti-virus.");
                    return;
                }

                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.UseShellExecute = false;
                startInfo.WorkingDirectory = $@"Packages\{game}";
                startInfo.Arguments = $@"a ""{output}"" ""{Path.GetFileName(path)}/*""";
                Console.WriteLine(startInfo.Arguments);
                Console.WriteLine($@"[INFO] Zipping {path} into {output}\{Path.GetFileName(path)}.7z");
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();
                }
                        
            });
        }

        private void ConvertCPK_Click(object sender, RoutedEventArgs e)
        {
            DisplayedMetadata row = (DisplayedMetadata)ModGrid.SelectedItem;
            foreach (var folder in Directory.GetDirectories($@"Packages\{game}\{row.path}"))
            {
                if (Path.GetFileName(folder).StartsWith("data0"))
                    MoveDirectory(folder, $@"Packages\{game}\{row.path}\{Path.GetFileNameWithoutExtension(cpkLang)}");
                else if (Path.GetFileName(folder).StartsWith("movie0"))
                    MoveDirectory(folder, $@"Packages\{game}\{row.path}\movie");
            }
            // Convert the mods.aem file too
            if (File.Exists($@"Packages\{game}\{row.path}\mods.aem"))
            {
                string text = File.ReadAllText($@"Packages\{game}\{row.path}\mods.aem");
                text = Regex.Replace(text, "data0000[0-6]", Path.GetFileNameWithoutExtension(cpkLang));
                File.WriteAllText($@"Packages\{game}\{row.path}\mods.aem", text);
            }
        }

        private void ModGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Space && ModGrid.CurrentColumn.Header.ToString() != "Enabled")
            {
                var checkbox = ModGrid.Columns[0].GetCellContent(ModGrid.SelectedItem) as CheckBox;
                checkbox.IsChecked = !checkbox.IsChecked;
            }
        }

        private void GameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameBox.SelectedIndex != -1 && IsLoaded)
            {
                int index = GameBox.SelectedIndex;
                game = null;
                switch (index)
                {
                    case 0:
                        game = "Persona 3 FES";
                        modPath = config.p3fConfig.modDir;
                        gamePath = config.p3fConfig.isoPath;
                        elfPath = config.p3fConfig.elfPath;
                        launcherPath = config.p3fConfig.launcherPath;
                        messageBox = config.p3fConfig.disableMessageBox;
                        useCpk = false;
                        ConvertCPK.Visibility = Visibility.Collapsed;
                        foreach (var button in buttons)
                        {
                            button.Foreground = new SolidColorBrush(Color.FromRgb(0x4f, 0xa4, 0xff));
                            button.IsHitTestVisible = true;
                        }
                        break;
                    case 1:
                        game = "Persona 4 Golden";
                        modPath = config.p4gConfig.modDir;
                        gamePath = config.p4gConfig.exePath;
                        launcherPath = config.p4gConfig.reloadedPath;
                        emptySND = config.p4gConfig.emptySND;
                        cpkLang = config.p4gConfig.cpkLang;
                        useCpk = config.p4gConfig.useCpk;
                        messageBox = config.p4gConfig.disableMessageBox;
                        ConvertCPK.Visibility = Visibility.Visible;
                        foreach (var button in buttons)
                        {
                            button.Foreground = new SolidColorBrush(Color.FromRgb(0xfe, 0xed, 0x2b));
                            button.IsHitTestVisible = true;
                        }
                        break;
                    case 2:
                        game = "Persona 5";
                        modPath = config.p5Config.modDir;
                        gamePath = config.p5Config.gamePath;
                        launcherPath = config.p5Config.launcherPath;
                        messageBox = config.p5Config.disableMessageBox;
                        useCpk = false;
                        ConvertCPK.Visibility = Visibility.Collapsed;
                        foreach (var button in buttons)
                        {
                            button.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x00));
                            button.IsHitTestVisible = true;
                        }
                        break;
                }
                config.game = game;
                if (modPath == "" || modPath == null)
                {
                    MergeButton.IsHitTestVisible = false;
                    MergeButton.Foreground = new SolidColorBrush(Colors.Gray);
                }
                LaunchButton.ToolTip = $"Launch {game}";
                if (!Directory.Exists($@"Packages\{game}"))
                {
                    Console.WriteLine($@"[INFO] Creating Packages\{game}");
                    Directory.CreateDirectory($@"Packages\{game}");
                }
                Console.WriteLine($"[INFO] Game set to {game}.");

                if (!Directory.Exists($@"Packages\{game}"))
                {
                    Console.WriteLine($@"[INFO] Creating Packages\{game}");
                    Directory.CreateDirectory($@"Packages\{game}");
                }

                PackageList.Clear();
                DisplayedPackages.Clear();

                if (File.Exists($@"Config\{game.Replace(" ", "")}Packages.xml"))
                {
                    try
                    {
                        using (FileStream streamWriter = File.Open($@"Config\{game.Replace(" ", "")}Packages.xml", FileMode.Open))
                        {
                            try
                            {
                                // Call the Deserialize method and cast to the object type.
                                packages = (Packages)xp.Deserialize(streamWriter);
                                PackageList = packages.packages;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($@"[ERROR] Couldn't deseralize Config\{game.Replace(" ", "")}Packages.xml ({ex.Message})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Invalid Packages.xml ({ex.Message})");
                    }
                }

                // Create displayed metadata from packages in PackageList and their respective Package.xml's
                foreach (var package in PackageList)
                {
                    string xml = $@"Packages\{game}\{package.path}\Package.xml";
                    Metadata m;
                    DisplayedMetadata dm = new DisplayedMetadata();
                    if (File.Exists(xml))
                    {
                        m = new Metadata();
                        try
                        {
                            using (FileStream streamWriter = File.Open(xml, FileMode.Open))
                            {
                                try
                                {
                                    m = (Metadata)xsp.Deserialize(streamWriter);
                                    dm.name = m.name;
                                    dm.id = m.id;
                                    dm.author = m.author;
                                    dm.version = m.version;
                                    dm.link = m.link;
                                    dm.description = m.description;
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path}. ({ex.Message}) Fix or delete the current Package.xml then refresh to use.");
                                    continue;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Invalid Package.xml for {package.path}. ({ex.Message}) Fix or delete the current Package.xml then refresh to use.");
                            continue;
                        }
                    }

                    dm.path = package.path;
                    dm.enabled = package.enabled;
                    DisplayedPackages.Add(dm);
                }
                ModGrid.ItemsSource = DisplayedPackages;

                Refresh();
                updateConfig();
                updatePackages();

                // Retrieve initial thumbnail from embedded resource
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream iconStream = asm.GetManifestResourceStream("AemulusModManager.Assets.Preview.png");
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = iconStream;
                bitmap.EndInit();
                Preview.Source = bitmap;

                Description.Document = ConvertToFlowDocument("Aemulus means \"Rival\" in Latin. It was chosen since it " +
                    "was made to rival Mod Compendium.\n\n(You are seeing this message because no package is selected or " +
                    "the package has no description.)");
            }

            
        }

        private void Kofi_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://www.ko-fi.com/tekka");
        }

        private void Setup_Click(object sender, MouseButtonEventArgs e)
        {
            if (File.Exists("Aemulus_Setup.pdf"))
                Process.Start("Aemulus_Setup.pdf");
            else
                Console.WriteLine("[ERROR] Aemulus_Setup.pdf not found.");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            outputter.Close();
        }

        private void FolderButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Directory.Exists($@"Packages\{game}"))
            {
                try
                {
                    ProcessStartInfo StartInformation = new ProcessStartInfo();
                    StartInformation.FileName = $@"Packages\{game}";
                    Process process = Process.Start(StartInformation);
                    Console.WriteLine($@"[INFO] Opened Packages\{game}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($@"[ERROR] Couldn't open Packages\{game} ({ex.Message})");
                }
            }
        }

        private void SwapButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            bottomUpPriority = !bottomUpPriority;
            if (!bottomUpPriority)
            {
                TopPriority.Text = "Higher Priority";
            }
            else
            {
                TopPriority.Text = "Lower Priority";
            }
            
            config.bottomUpPriority = bottomUpPriority;
            updateConfig();
            for (int i = 0; i < DisplayedPackages.Count; i++)
            {
                DisplayedPackages.Move(DisplayedPackages.Count - 1, i);
                PackageList.Move(PackageList.Count - 1, i);
            }
            updatePackages();
            Console.WriteLine("[INFO] Switched priority.");

        }

        private void MouseEnterColorChange(object sender, MouseEventArgs e)
        {
            var button = e.OriginalSource as FontAwesome5.ImageAwesome;
            if (button.IsHitTestVisible)
            {
                switch (game)
                {
                    case "Persona 3 FES":
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0x28, 0x52, 0x80));
                        break;
                    case "Persona 4 Golden":
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x77, 0x1a));
                        break;
                    case "Persona 5":
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x00, 0x00));
                        break;
                }
            }
        }

        private void MouseLeaveColorChange(object sender, MouseEventArgs e)
        {
            var button = e.OriginalSource as FontAwesome5.ImageAwesome;
            if (button.IsHitTestVisible)
            {
                switch (game)
                {
                    case "Persona 3 FES":
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0x4f, 0xa4, 0xff));
                        break;
                    case "Persona 4 Golden":
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0xfe, 0xed, 0x2b));
                        break;
                    case "Persona 5":
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x00));
                        break;
                }
            }
        }

        public void setAttributesNormal(DirectoryInfo dir)
        {
            foreach (var subDir in dir.GetDirectories())
            {
                setAttributesNormal(subDir);
                subDir.Attributes = FileAttributes.Normal;
            }
            foreach (var file in dir.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;
            }
        }

        public static void DeleteDirectory(string path)
        {

            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDirectory(directory);
            }
            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
        }

        private void Add_Enter(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.Move;
        }

        private async Task ExtractPackages(string[] fileList)
        {
            await Task.Run(() =>
            {
                bool dropped = false;
                foreach (var file in fileList)
                {
                    if (Directory.Exists(file))
                    {
                        Console.WriteLine($@"[INFO] Moving {file} into Packages\{game}");
                        MoveDirectory(file, $@"Packages\{game}\{Path.GetFileName(file)}");
                        dropped = true;
                    }
                    else if (Path.GetExtension(file).ToLower() == ".7z" || Path.GetExtension(file).ToLower() == ".rar" || Path.GetExtension(file).ToLower() == ".zip")
                    {
                        Directory.CreateDirectory("temp");
                        ProcessStartInfo startInfo = new ProcessStartInfo();
                        startInfo.CreateNoWindow = true;
                        startInfo.FileName = @"Dependencies\7z\7z.exe";
                        if (!File.Exists(startInfo.FileName))
                        {
                            Console.Write($"[ERROR] Couldn't find {startInfo.FileName}. Please check if it was blocked by your anti-virus.");
                            return;
                        }

                        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        startInfo.UseShellExecute = false;
                        startInfo.Arguments = $@"x -y ""{file}"" -otemp";
                        Console.WriteLine($@"[INFO] Extracting {file} into Packages\{game}");
                        using (Process process = new Process())
                        {
                            process.StartInfo = startInfo;
                            process.Start();
                            process.WaitForExit();
                        }
                        // Put in folder if extraction comes in multiple files/folders
                        if (Directory.GetFileSystemEntries("temp").Length > 1)
                        {
                            setAttributesNormal(new DirectoryInfo("temp"));
                            MoveDirectory("temp", $@"Packages\{game}\{Path.GetFileNameWithoutExtension(file)}");
                        }
                        // Move folder if extraction is just a folder
                        else if (Directory.GetFileSystemEntries("temp").Length == 1 && Directory.Exists(Directory.GetFileSystemEntries("temp")[0]))
                        {
                            setAttributesNormal(new DirectoryInfo("temp"));
                            MoveDirectory(Directory.GetFileSystemEntries("temp")[0], $@"Packages\{game}\{Path.GetFileNameWithoutExtension(Directory.GetFileSystemEntries("temp")[0])}");
                        }
                        //File.Delete(file);
                        dropped = true;
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] {file} isn't a folder, .zip, .7z, or .rar, skipping...");
                    }
                }
                if (Directory.Exists("temp"))
                    DeleteDirectory("temp");

                if (dropped)
                {
                    Refresh();
                    updatePackages();
                }
            });
        }

        private async void Add_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] fileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                });

                foreach (var button in buttons)
                {
                    button.IsHitTestVisible = false;
                    button.Foreground = new SolidColorBrush(Colors.Gray);
                }
                GameBox.IsHitTestVisible = false;
                ModGrid.IsHitTestVisible = false;

                await ExtractPackages(fileList);

                
                ModGrid.IsHitTestVisible = true;
                foreach (var button in buttons)
                {
                    button.IsHitTestVisible = true;
                    if (game == "Persona 3 FES")
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0x4f, 0xa4, 0xff));
                    else if (game == "Persona 4 Golden")
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0xfe, 0xed, 0x2b));
                    else
                        button.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x00));
                }
                GameBox.IsHitTestVisible = true;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Mouse.OverrideCursor = null;
                });
            }
        }

    }
}