using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Session;
using System.Collections.Generic;


namespace VolumeWheelFE
{
    public partial class Form1 : Form
    {
        static CoreAudioDevice defaultPlaybackDevice = new CoreAudioController().DefaultPlaybackDevice;
        static bool buttonPreviouslyPressed = false;
        static DateTime lastButtonPressTime = DateTime.MinValue;
        static DateTime?[] buttonPressStarts = new DateTime?[4];
        static readonly TimeSpan debounceTime = TimeSpan.FromMilliseconds(50);
        static int lastEncoderValue = 0;
        CoreAudioController audioController = new CoreAudioController();
        DeviceSessionPair[] selectedPairs = new DeviceSessionPair[4];
        IAudioSession[] selectedApp = new IAudioSession[4];
        int[] lastPosition = { -333, -333, -333, -333 };
        List<int>[] processIds = new List<int>[4];
        int[] chosenSessions = new int[4];
        List<IAudioSession> audioSessions = new List<IAudioSession> { };

        public Form1()
        {
            processIds[0] = new List<int> { };
            processIds[1] = new List<int> { };
            processIds[2] = new List<int> { };
            processIds[3] = new List<int> { };
            InitializeComponent();
            PopulateSessions();
        }

        private void PopulateSessions()
        {
            var devices = audioController.GetPlaybackDevices(AudioSwitcher.AudioApi.DeviceState.Active);
            ComboBox[] comboBoxes = { comboBox2, comboBox3, comboBox11, comboBox13 };

            foreach (var device in devices)
            {
                foreach (var session in device.SessionController.All())
                {
                    if (session != null && !session.IsSystemSession)
                    {
                        audioSessions.Add(session);
                    }
                }
            }

            var uniqueSessions = audioSessions
                .GroupBy(session => string.IsNullOrWhiteSpace(session.DisplayName) ? session.ProcessId.ToString() : session.DisplayName)
                .Select(group => group.First())
                .ToList();
            foreach (var comboBox in comboBoxes)
            {
                comboBox.Items.Clear();
                foreach (var session in uniqueSessions)
                {
                    comboBox.Items.Add(session);
                }
            }
        }


        private void comboBox2_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is IAudioSession session)
            {
                e.Value = generateCleanSessionName(session);
                chosenSessions[0] = session.ProcessId;
            }
        }
        private void comboBox3_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is IAudioSession session)
            {
                e.Value = generateCleanSessionName(session);
                chosenSessions[1] = session.ProcessId;
            }
        }
        private void comboBox13_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is IAudioSession session)
            {
                e.Value = generateCleanSessionName(session);
                chosenSessions[2] = session.ProcessId;
            }
        }
        private void comboBox11_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is IAudioSession session)
            {
                e.Value = generateCleanSessionName(session);
                chosenSessions[3] = session.ProcessId;
            }
        }
        private string generateCleanSessionName(IAudioSession session)
        {
            string sessionName;
            try
            {
                Process process = Process.GetProcessById(session.ProcessId);
                sessionName = process.ProcessName;
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine("Process not found: " + ex.Message);
                sessionName = string.IsNullOrWhiteSpace(session.DisplayName) ? "Unnamed Session" : session.DisplayName;
            }
            return sessionName;
        }


        private void button1_Click(object sender, EventArgs e)
        {
            selectedApp[0] = (IAudioSession)comboBox2.SelectedItem;
            selectedApp[1] = (IAudioSession)comboBox3.SelectedItem;
            selectedApp[2] = (IAudioSession)comboBox11.SelectedItem;
            selectedApp[3] = (IAudioSession)comboBox13.SelectedItem;
            start_app();
        }

        private void start_app()
        {
            string arduinoPort = DetectArduinoPort();
            if (arduinoPort == null)
            {
                Console.WriteLine("Arduino not found.");
                return;
            }
            using (SerialPort arduino = new SerialPort(arduinoPort, 9600))
            {
                arduino.Open();
                while (true)
                {
                    try
                    {
                        string data = arduino.ReadLine().Trim();
                        HandleArduinoInput(data);
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine("Arduino Timeout");
                    }
                }
            }

        }


        private void HandleArduinoInput(string input)
        {
            // Input format is "Encoder#:Value" or "Encoder#::P" or "Encoder#::R"
            var parts = input.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int encoderIndex) && int.TryParse(parts[1], out int newPosition))
            {
                if (encoderIndex >= 0 && encoderIndex < selectedApp.Length)
                {
                    if (lastPosition[encoderIndex] == -333)
                    {
                        lastPosition[encoderIndex] = newPosition;

                    }
                    int volumeChange = newPosition - lastPosition[encoderIndex];
                    AdjustSessionVolume(selectedApp[encoderIndex], volumeChange);
                    lastPosition[encoderIndex] = newPosition;
                }
            }
            else if (parts.Length == 3 && int.TryParse(parts[0], out int encoderIndex2))
            {
                if (parts[2] == "P")
                {
                    if (!buttonPressStarts[encoderIndex2].HasValue)
                    {
                        buttonPressStarts[encoderIndex2] = DateTime.Now;
                    }
                }
                else if (parts[2] == "R")
                {
                    if (buttonPressStarts[encoderIndex2].HasValue && (DateTime.Now - buttonPressStarts[encoderIndex2].Value) > debounceTime)
                    {
                        foreach (var session in audioSessions)
                        {
                            if (session.ProcessId == selectedApp[encoderIndex2].ProcessId || session.DisplayName == selectedApp[encoderIndex2].DisplayName && session.DisplayName != "")
                            {
                                session.IsMuted = !session.IsMuted;
                            }
                        }
                    }

                }
            }
        }

        private void AdjustSessionVolume(IAudioSession app, int volumeChange)
        {
            foreach (var session in audioSessions)
            {
                if (session.ProcessId == app.ProcessId || session.DisplayName == app.DisplayName && session.DisplayName != "")
                {
                    double currentVolume = session.Volume;
                    double newVolume = Math.Max(0, Math.Min(100, currentVolume + volumeChange));
                    session.Volume = newVolume;
                }
            }
        }

        private string DetectArduinoPort()
        {
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
            {
                SerialPort testPort = new SerialPort(port, 9600);
                try
                {
                    testPort.Open();
                    testPort.WriteLine("VolumeWheel Arduino Check");
                    testPort.ReadTimeout = 5000; // 5 seconds timeout

                    try
                    {
                        string response = testPort.ReadLine();
                        if (response.Trim() == "VolumeWheel Arduino Online")
                        {
                            testPort.Close();
                            return port;
                        }
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine("Timeout");
                    }

                    testPort.Close();
                }
                catch
                {
                    Console.WriteLine("error");
                }
            }
            return null;
        }
    }
}
