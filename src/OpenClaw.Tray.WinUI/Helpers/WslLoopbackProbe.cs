using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace OpenClawTray.Helpers;

/// <summary>
/// Detects whether a loopback TCP port is being served by Windows' WSL relay
/// (wslrelay.exe) — i.e., a gateway running inside a WSL2 distro, surfaced on
/// Windows via the localhost forwarder. Used to refine
/// <c>GatewayTopologyClassifier</c> output so the topology label says "WSL"
/// instead of "Windows native" when the loopback target is actually a WSL
/// process.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WslLoopbackProbe
{
    private const int AF_INET = 2;
    private const uint TCP_TABLE_OWNER_PID_LISTENER = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public byte localPort1;
        public byte localPort2;
        public byte localPort3;
        public byte localPort4;
        public uint remoteAddr;
        public byte remotePort1;
        public byte remotePort2;
        public byte remotePort3;
        public byte remotePort4;
        public uint owningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        uint tblClass,
        uint reserved);

    /// <summary>
    /// Returns <c>true</c> when the loopback TCP listener on <paramref name="port"/>
    /// is owned by <c>wslrelay.exe</c>. Returns <c>false</c> for any other owner,
    /// or when ownership cannot be determined (caller should treat that as
    /// "unknown" and fall back to the URL-based classification).
    /// </summary>
    public static bool IsLoopbackPortOwnedByWslRelay(int port)
    {
        if (port <= 0 || port > ushort.MaxValue)
        {
            return false;
        }

        int? ownerPid = TryGetLoopbackListenerPid(port);
        if (ownerPid is not int pid)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            // MainModule can throw for protected processes; ProcessName is reliable enough.
            var name = process.ProcessName;
            return name.Equals("wslrelay", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static int? TryGetLoopbackListenerPid(int port)
    {
        int bufferSize = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0);
        if (bufferSize <= 0)
        {
            return null;
        }

        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            uint result = GetExtendedTcpTable(buffer, ref bufferSize, true, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0);
            if (result != 0)
            {
                return null;
            }

            int rowCount = Marshal.ReadInt32(buffer);
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            IntPtr rowPtr = IntPtr.Add(buffer, sizeof(int));

            var loopback4 = IPAddress.Loopback.GetAddressBytes();
            for (int i = 0; i < rowCount; i++)
            {
                var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr);
                rowPtr = IntPtr.Add(rowPtr, rowSize);

                int rowPort = (row.localPort1 << 8) | row.localPort2;
                if (rowPort != port)
                {
                    continue;
                }

                var addrBytes = BitConverter.GetBytes(row.localAddr);
                bool isLoopback =
                    (addrBytes[0] == loopback4[0] && addrBytes[1] == loopback4[1] &&
                     addrBytes[2] == loopback4[2] && addrBytes[3] == loopback4[3]) ||
                    // 0.0.0.0 / dual-stack listener that also covers loopback.
                    (addrBytes[0] == 0 && addrBytes[1] == 0 && addrBytes[2] == 0 && addrBytes[3] == 0);

                if (isLoopback)
                {
                    return (int)row.owningPid;
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return null;
    }
}
