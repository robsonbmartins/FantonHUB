using System;
using System.Collections.Generic;
using Vanara.PInvoke;
using Serilog;
using System.Runtime.InteropServices;
using static Vanara.PInvoke.SetupAPI;

namespace CmcMidiRouter;

public class UsbDeviceInfo
{
    public string InstanceId { get; set; } = string.Empty;
    public string Vid { get; set; } = string.Empty;
    public string Pid { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    
    public override string ToString() => $"VID_{Vid}&PID_{Pid} [{SerialNumber}] - {FriendlyName}";
}

public class DeviceDiscovery
{
    // GUID for USB Device Class
    private static readonly Guid UsbClassGuid = new Guid("36FC9E60-C465-11CF-8056-444553540000");

    public static List<UsbDeviceInfo> EnumerateCmcDevices()
    {
        var devices = new List<UsbDeviceInfo>();
        
        using var devInfoSet = SetupDiGetClassDevs(UsbClassGuid, null, HWND.NULL, DIGCF.DIGCF_PRESENT);
        if (devInfoSet.IsInvalid)
        {
            Log.Error("SetupDiGetClassDevs falhou.");
            return devices;
        }

        uint index = 0;
        SP_DEVINFO_DATA devInfoData = new SP_DEVINFO_DATA();
        devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

        while (SetupDiEnumDeviceInfo(devInfoSet, index, ref devInfoData))
        {
            index++;
            
            System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
            if (SetupDiGetDeviceInstanceId(devInfoSet, devInfoData, sb, (uint)sb.Capacity, out uint reqSize))
            {
                string instanceId = sb.ToString();

                // Yamaha/Steinberg VID is 0499. CMC-FD PID is 150F
                if (instanceId.IndexOf("VID_0499", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var info = new UsbDeviceInfo { InstanceId = instanceId, FriendlyName = "Steinberg CMC Device" };
                    
                    var parts = instanceId.Split('\\');
                    if (parts.Length >= 3)
                    {
                        info.SerialNumber = parts[2];
                    }
                    
                    if (parts.Length >= 2)
                    {
                        var idParts = parts[1].Split('&');
                        foreach(var p in idParts)
                        {
                            if (p.StartsWith("VID_")) info.Vid = p.Substring(4);
                            if (p.StartsWith("PID_")) info.Pid = p.Substring(4);
                        }
                    }

                    devices.Add(info);
                    // Log.Debug($"Dispositivo CMC Encontrado via SetupAPI: {info}");
                }
            }
        }
        
        return devices;
    }
}
