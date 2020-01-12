using AudioPipe.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace MultistreamAudio
{
    class Program
    {
        static void Main(string[] args)
        {
            var exitEvent = new ManualResetEvent(false);
            

            var outputDevices = DeviceService.GetOutputDevices().ToList();
            var defaultOutputDevice = DeviceService.DefaultPlaybackDevice;

            Console.WriteLine("Enter part of or the whole name of an output device to assign as the left speaker.");
            var leftDeviceName = Console.ReadLine();
            var leftDevice = outputDevices.Single(device => device.FriendlyName.ToLower().Contains(leftDeviceName.ToLower()));

            Console.WriteLine("Enter part of or the whole name of an output device to assign as the right speaker.");
            var rightDeviceName = Console.ReadLine();
            var rightDevice = outputDevices.Single(device => device.FriendlyName.ToLower().Contains(rightDeviceName.ToLower()));

            Console.WriteLine("Connecting processing pipeline...");
            var pipe = new MultiOutputAudioPipe(
                defaultOutputDevice, 
                new MultiChannelList<MMDevice>(
                    leftDevice,
                    rightDevice
                )
            );
            pipe.MuteInputWhenPiped = true;

            Console.CancelKeyPress += (sender, eventArgs) => {
                eventArgs.Cancel = true;
                exitEvent.Set();
                pipe.Stop();
                Console.WriteLine("Stopped pipeline.");
            };
            AppDomain.CurrentDomain.ProcessExit += new EventHandler((sender, eventArgs) =>
            {
                exitEvent.Set();
                pipe.Stop();
                Console.WriteLine("Stopped pipeline.");
            });

            Console.WriteLine("Pipeline connected.");
            pipe.Start();
            Console.WriteLine("Pipeline started.");

            exitEvent.WaitOne();
        }
    }
}
