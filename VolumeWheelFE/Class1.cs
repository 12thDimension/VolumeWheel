using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VolumeWheelFE
{
    internal class DeviceSessionPair
    {
        public CoreAudioDevice Device { get; set; }
        public IAudioSession Session { get; set; } 
    }
}
