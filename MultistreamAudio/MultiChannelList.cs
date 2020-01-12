using NAudio.CoreAudioApi;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace MultistreamAudio
{
    public class MultiChannelList<T> : IEnumerable<T>, ICollection<T>
    {
        public List<T> Devices { get; private set; }

        public T RightDevice => Devices[0];

        public T LeftDevice => Devices[1];        

        public int Count => Devices.Count;

        public bool IsReadOnly => true;

        public MultiChannelList(T rightDevice, T leftDevice)
        {
            Devices = new List<T> { rightDevice, leftDevice };
        }

        public MultiChannelList(IEnumerable<T> list)
        {
            Devices = new List<T>(list);
            if (Devices.Count != 2)
            {
                throw new ArgumentException("Two devices are needed for multichannel.");
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Devices.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item) => Devices.Add(item);

        public void Clear() => Devices.Clear();

        public bool Contains(T item) => Devices.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => Devices.CopyTo(array, arrayIndex);

        public bool Remove(T item) => Devices.Remove(item);
    }
}
