using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace RemoteCamera
{
    /// <summary>
    /// Windows に登録されているカメラデバイスを取得するサービス。
    /// </summary>
    internal sealed class CameraDeviceCatalog
    {
        private static readonly Guid VideoInputDeviceCategory = new("860BB310-5D01-11D0-BD3B-00A0C911CE86");

        /// <summary>
        /// カメラ候補を取得する。
        /// </summary>
        /// <returns>カメラ候補一覧。</returns>
        public IReadOnlyList<CameraDeviceOption> GetCameraDevices()
        {
            var devices = GetDirectShowDevices();
            if (devices.Count > 0)
            {
                return devices;
            }

            return GetFallbackDevices();
        }

        /// <summary>
        /// DirectShow に登録されている映像入力デバイスを取得する。
        /// </summary>
        /// <returns>カメラ候補一覧。</returns>
        private static List<CameraDeviceOption> GetDirectShowDevices()
        {
            var devices = new List<CameraDeviceOption>();
            ICreateDevEnum? deviceEnumerator = null;
            IEnumMoniker? enumMoniker = null;

            try
            {
                var systemDeviceEnumType = Type.GetTypeFromCLSID(new Guid("62BE5D10-60EB-11D0-BD3B-00A0C911CE86"));
                if (systemDeviceEnumType is null)
                {
                    return devices;
                }

                deviceEnumerator = (ICreateDevEnum)Activator.CreateInstance(systemDeviceEnumType)!;
                var category = VideoInputDeviceCategory;
                var result = deviceEnumerator.CreateClassEnumerator(ref category, out enumMoniker, 0);
                if (result != 0 || enumMoniker is null)
                {
                    return devices;
                }

                var monikers = new IMoniker[1];
                while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
                {
                    var displayName = ReadFriendlyName(monikers[0]) ?? $"カメラ {devices.Count}";
                    devices.Add(new CameraDeviceOption(displayName, devices.Count, CameraSourceType.DeviceIndex));
                    Marshal.ReleaseComObject(monikers[0]);
                }
            }
            catch
            {
                devices.Clear();
            }
            finally
            {
                if (enumMoniker is not null)
                {
                    Marshal.ReleaseComObject(enumMoniker);
                }

                if (deviceEnumerator is not null)
                {
                    Marshal.ReleaseComObject(deviceEnumerator);
                }
            }

            return devices;
        }

        /// <summary>
        /// デバイス名を取得する。
        /// </summary>
        /// <param name="moniker">DirectShow のデバイス識別子。</param>
        /// <returns>デバイス名。</returns>
        private static string? ReadFriendlyName(IMoniker moniker)
        {
            IPropertyBag? propertyBag = null;

            try
            {
                var propertyBagId = typeof(IPropertyBag).GUID;
                moniker.BindToStorage(null!, null!, ref propertyBagId, out var storage);
                propertyBag = storage as IPropertyBag;
                if (propertyBag is null)
                {
                    return null;
                }

                var result = propertyBag.Read("FriendlyName", out var value, IntPtr.Zero);
                return result == 0 ? value as string : null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (propertyBag is not null)
                {
                    Marshal.ReleaseComObject(propertyBag);
                }
            }
        }

        /// <summary>
        /// DirectShow 一覧が取得できない場合の候補を作成する。
        /// </summary>
        /// <returns>カメラ候補一覧。</returns>
        private static IReadOnlyList<CameraDeviceOption> GetFallbackDevices()
        {
            var devices = new List<CameraDeviceOption>();
            for (var index = 0; index < 10; index++)
            {
                devices.Add(new CameraDeviceOption($"カメラ {index}", index, CameraSourceType.DeviceIndex));
            }

            return devices;
        }

        [ComImport]
        [Guid("29840822-5B84-11D0-BD3B-00A0C911CE86")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICreateDevEnum
        {
            int CreateClassEnumerator(ref Guid type, out IEnumMoniker? enumMoniker, int flags);
        }

        [ComImport]
        [Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyBag
        {
            int Read(
                [MarshalAs(UnmanagedType.LPWStr)] string propertyName,
                [MarshalAs(UnmanagedType.Struct)] out object value,
                IntPtr errorLog);
        }
    }
}
