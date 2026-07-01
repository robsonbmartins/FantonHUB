using System;

namespace CmcMidiRouter;

public static class MidiTranslator
{
    public static string Translate(byte[] data)
    {
        if (data == null || data.Length == 0) return "Empty";

        byte status = data[0];
        byte cmd = (byte)(status & 0xF0);
        byte ch = (byte)((status & 0x0F) + 1);

        if (status == 0xF0) return "SysEx Message (" + data.Length + " bytes)";
        if (status == 0xF8) return "Timing Clock";
        if (status == 0xFA) return "Start";
        if (status == 0xFB) return "Continue";
        if (status == 0xFC) return "Stop";
        if (status == 0xFE) return "Active Sensing";
        if (status == 0xFF) return "System Reset";

        if (data.Length >= 2)
        {
            byte d1 = data[1];
            
            if (cmd == 0x80) return $"Note Off, Ch {ch}, Note {d1}, Vel {(data.Length > 2 ? data[2] : 0)}";
            if (cmd == 0x90) 
            {
                byte vel = data.Length > 2 ? data[2] : (byte)0;
                if (vel == 0) return $"Note Off, Ch {ch}, Note {d1}, Vel 0";
                return $"Note On, Ch {ch}, Note {d1}, Vel {vel}";
            }
            if (cmd == 0xA0) return $"Poly Aftertouch, Ch {ch}, Note {d1}, Val {(data.Length > 2 ? data[2] : 0)}";
            if (cmd == 0xB0) return $"Control Change, Ch {ch}, CC {d1}, Val {(data.Length > 2 ? data[2] : 0)}";
            if (cmd == 0xC0) return $"Program Change, Ch {ch}, Prog {d1}";
            if (cmd == 0xD0) return $"Channel Aftertouch, Ch {ch}, Val {d1}";
            if (cmd == 0xE0) 
            {
                int pitch = d1;
                if (data.Length > 2) pitch |= (data[2] << 7);
                return $"Pitch Bend, Ch {ch}, Val {pitch}";
            }
        }

        return "Unknown (" + BitConverter.ToString(data) + ")";
    }
}
