using System;
using System.IO.Ports;
using AudioSwitcher.AudioApi.Session;
using System.Collections.Generic;
using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;


class Program
{
    static List<IAudioSession> audioSessions = new List<IAudioSession> { };
    static int[] lastPosition = { -333, -333, -333, -333 };
    static DateTime?[] buttonPressStarts = new DateTime?[4];
    static TimeSpan debounceTime = TimeSpan.FromMilliseconds(50);


    static void Main(string[] args)
    {
        string arduinoPort = DetectArduinoPort();
        if (arduinoPort == null)
        {
            Console.WriteLine("Arduino not found.");
            return;
        }
        CoreAudioController audioController = new CoreAudioController();
        var devices = audioController.GetPlaybackDevices(AudioSwitcher.AudioApi.DeviceState.Active);
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

        using (SerialPort arduino = new SerialPort(arduinoPort, 9600))
        {
            arduino.Open();
            while (true)
            {
                string data = arduino.ReadLine().Trim();
                HandleArduinoInput(data);
            }
        }
    }

    static void HandleArduinoInput(string input)
    {
        // Input format is "Encoder#:Value" or "Encoder#::P" or "Encoder#::R"
        var parts = input.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out int encoderIndex) && int.TryParse(parts[1], out int newPosition))
        {
            if (lastPosition[encoderIndex] == -333)
            {
                lastPosition[encoderIndex] = newPosition;

            }
            int volumeChange = newPosition - lastPosition[encoderIndex];
            AdjustSessionVolume(encoderIndex, volumeChange);
            lastPosition[encoderIndex] = newPosition;

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
                        if (isCorrectSelectedApp(session.Id, encoderIndex2))
                        {
                            session.IsMuted = !session.IsMuted;
                        }
                    }
                }

            }
        }
    }

    static void AdjustSessionVolume(int encoderIndex, int volumeChange)
    {
        foreach (var session in audioSessions)
        {
            if (isCorrectSelectedApp(session.Id, encoderIndex))
            {
                double currentVolume = session.Volume;
                double newVolume = Math.Max(0, Math.Min(100, currentVolume + volumeChange));
                session.Volume = newVolume;
            }
        }
    }

    static bool isCorrectSelectedApp(string sessionId, int encoderIndex)
    {
        List<string> selectedApps = new List<string> { "", "Discord.exe", "vivaldi.exe", "DeadByDaylight-Win64-Shipping.exe" };
        int lastBackslashIndex = sessionId.LastIndexOf("\\");
        int percentBIndex = sessionId.IndexOf("%b");
        if (lastBackslashIndex != -1 && percentBIndex != -1)
        {
            int start = lastBackslashIndex + 1;
            int length = percentBIndex - start;
            var a = sessionId.Substring(start, length).Trim();
            if (sessionId.Substring(start, length).Trim() == selectedApps[encoderIndex])
            {
                return true;
            }
        }
        return false;
    }

    static string DetectArduinoPort()
    {
        string[] ports = SerialPort.GetPortNames();
        foreach (string port in ports)
        {
            SerialPort testPort = new SerialPort(port, 9600);

            testPort.Open();
            testPort.WriteLine("VolumeWheel Arduino Check");
            testPort.ReadTimeout = 5000; // 5 seconds timeout

            string response = testPort.ReadLine();
            if (response.Trim() == "VolumeWheel Arduino Online")
            {
                testPort.Close();
                return port;
            }
        }
        return null;
    }
}