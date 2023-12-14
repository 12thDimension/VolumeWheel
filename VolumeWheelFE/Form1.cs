using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Windows.Forms;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Session;

namespace VolumeWheelFE
{
    public partial class Form1 : Form
    {
        static CoreAudioDevice defaultPlaybackDevice = new CoreAudioController().DefaultPlaybackDevice;
        static bool buttonPreviouslyPressed = false;
        static DateTime lastButtonPressTime = DateTime.MinValue;
        static DateTime? buttonPressStart = null;
        static readonly TimeSpan debounceTime = TimeSpan.FromMilliseconds(50);
        static int lastEncoderValue = 0;
        CoreAudioController audioController = new CoreAudioController();
        DeviceSessionPair[] selectedPairs = new DeviceSessionPair[4];
        int[] lastPosition = [0,0,0,0];
        bool justStarted = true;

        public Form1()
        {
            InitializeComponent();
            PopulateAudioDevices();
            //ListAudioSessions();
        }

        private void PopulateAudioDevices()
        {
            var devices = audioController.GetPlaybackDevices(AudioSwitcher.AudioApi.DeviceState.Active);
            foreach (var device in devices)
            {
                comboBox1.Items.Add(device);
                comboBox4.Items.Add(device);
                comboBox14.Items.Add(device);
                comboBox12.Items.Add(device);

                comboBox9.Items.Add(device.FullName);
                comboBox10.Items.Add(device.FullName);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var device = comboBox1.SelectedItem as CoreAudioDevice;
            PopulateSessions(device, comboBox2);
        }

        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            var device = comboBox4.SelectedItem as CoreAudioDevice;
            PopulateSessions(device, comboBox3);
        }

        private void comboBox14_SelectedIndexChanged(object sender, EventArgs e)
        {
            var device = comboBox14.SelectedItem as CoreAudioDevice;
            PopulateSessions(device, comboBox13);
        }
        private void comboBox12_SelectedIndexChanged(object sender, EventArgs e)
        {
            var device = comboBox12.SelectedItem as CoreAudioDevice;
            PopulateSessions(device, comboBox11);
        }

        private void PopulateSessions(CoreAudioDevice device, ComboBox sessionComboBox)
        {
            sessionComboBox.Items.Clear();

            if (device != null)
            {
                foreach (var session in device.SessionController.All())
                {
                    if (!session.IsSystemSession)
                    {
                        string sessionName;
                        try
                        {
                            Process process = Process.GetProcessById(session.ProcessId);
                            sessionName = process.ProcessName;
                        }
                        catch (ArgumentException ex)
                        {
                            // Process with specified ID does not exist
                            Console.WriteLine("Process not found: " + ex.Message);
                            sessionName = string.IsNullOrWhiteSpace(session.DisplayName) ? "Unnamed Session" : session.DisplayName;
                        }
                        sessionComboBox.Items.Add(sessionName);
                    }
                }
            }

            if (sessionComboBox.Items.Count == 0)
            {
                sessionComboBox.Items.Add("No active sessions");
            }
        }

        private void comboBox1_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is CoreAudioDevice device)
            {
                e.Value = device.FullName;
            }
        }
        private void comboBox4_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is CoreAudioDevice device)
            {
                e.Value = device.FullName;
            }
        }
        private void comboBox14_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is CoreAudioDevice device)
            {
                e.Value = device.FullName;
            }
        }
        private void comboBox12_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is CoreAudioDevice device)
            {
                e.Value = device.FullName;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            selectedPairs[0] = new DeviceSessionPair { Device = comboBox1.SelectedItem as CoreAudioDevice, Session = GetSessionFromName(comboBox1, comboBox2.SelectedItem as string) };
            selectedPairs[1] = new DeviceSessionPair { Device = comboBox4.SelectedItem as CoreAudioDevice, Session = GetSessionFromName(comboBox4, comboBox3.SelectedItem as string) };
            selectedPairs[2] = new DeviceSessionPair { Device = comboBox14.SelectedItem as CoreAudioDevice, Session = GetSessionFromName(comboBox14, comboBox13.SelectedItem as string) };
            selectedPairs[3] = new DeviceSessionPair { Device = comboBox12.SelectedItem as CoreAudioDevice, Session = GetSessionFromName(comboBox12, comboBox11.SelectedItem as string) };
            // ... Repeat for other device/session pairs
            start_app();
        }

        private void start_app()
        {
            string arduinoPort = DetectArduinoPort();
            if (arduinoPort != null)
            {
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
                        catch (TimeoutException) { }
                    }
                }
            }
            else
            {
                Console.WriteLine("Arduino not found.");
            }
        }

        private IAudioSession GetSessionFromName(ComboBox deviceComboBox, string sessionName)
        {
            var device = deviceComboBox.SelectedItem as CoreAudioDevice;
            if (device != null)
            {
                foreach (var session in device.SessionController.All())
                {
                    if (session.DisplayName == sessionName)
                    {
                        return session;
                    }
                }
            }
            return null;
        }

        private void HandleArduinoInput(string input)
        {
            // Assuming input format is "Encoder#:Value"
            var parts = input.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int encoderIndex) && int.TryParse(parts[1], out int volumeChange))
            {
                if (encoderIndex >= 0 && encoderIndex < selectedPairs.Length)
                {
                    if (justStarted)
                    {
                        justStarted = false;
                        
                    }
                    AdjustSessionVolume(selectedPairs[encoderIndex], volumeChange);
                }
            }
            // ... existing button press/release handling

            //if (input == "Button Pressed")
            //{
            //    if (!buttonPressStart.HasValue)
            //    {
            //        buttonPressStart = DateTime.Now;
            //    }
            //}
            //else if (input == "Button Released")
            //{
            //    if (buttonPressStart.HasValue && (DateTime.Now - buttonPressStart.Value) > debounceTime)
            //    {
            //        // Toggle mute
            //        defaultPlaybackDevice.Mute(!defaultPlaybackDevice.IsMuted);
            //    }
            //    buttonPressStart = null;
            //}
            //else if (int.TryParse(input, out int encoderValue))
            //{
            //    // Calculate the change in encoder value
            //    lastEncoderValue = encoderValue;

            //    // Adjust volume by 1% per 4 encoder units
            //    double currentVolume = defaultPlaybackDevice.Volume;
            //    double newVolume = Math.Max(0, Math.Min(100, currentVolume + (encoderValue - lastEncoderValue)));
            //    defaultPlaybackDevice.Volume = newVolume;
            //}
        }
        static void AdjustSessionVolume(DeviceSessionPair pair, int volumeChange)
        {
            if (pair != null && pair.Session != null)
            {
                double currentVolume = pair.Session.Volume;
                double newVolume = Math.Max(0, Math.Min(100, currentVolume + (volumeChange / 4.0)));
                pair.Session.Volume = newVolume;
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
                            return port; // Return the port if Arduino is detected
                        }
                    }
                    catch (TimeoutException)
                    {
                        Console.WriteLine("Timeout");
                        // Timeout occurred, no response from Arduino
                    }

                    testPort.Close();
                }
                catch
                {
                    Console.WriteLine("error");
                    // Handle error or continue testing other ports
                }
            }
            return null; // Return null if no Arduino is detected
        }
    }
}
