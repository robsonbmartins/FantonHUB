using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Midi;
using Serilog;
using TobiasErichsen.teVirtualMIDI;
using System.Linq;

namespace CmcMidiRouter;

public class MidiMessageEventArgs : EventArgs
{
    public string SourcePort { get; set; } = "";
    public string DestPort { get; set; } = "";
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; }
}

public class MidiRouter : IDisposable
{
    private Dictionary<string, TeVirtualMIDI> _virtualPorts = new Dictionary<string, TeVirtualMIDI>();
    private CancellationTokenSource? _cancellation;
    private List<Task> _readTasks = new List<Task>();
    
    private Dictionary<string, List<MidiIn>> cmcInputs = new Dictionary<string, List<MidiIn>>();
    private Dictionary<string, List<MidiOut>> cmcOutputs = new Dictionary<string, List<MidiOut>>();
    private Dictionary<MidiIn, string> _midiInNames = new Dictionary<MidiIn, string>();

    public List<string> ConnectedInputNames { get; private set; } = new List<string>();
    public List<string> ConnectedOutputNames { get; private set; } = new List<string>();

    public event EventHandler? DevicesRefreshed;
    public event EventHandler<MidiMessageEventArgs>? MessageRouted;
    
    public bool IsMonitoring { get; set; } = false;

    public void StartRouting()
    {
        Log.Information("Iniciando roteamento MIDI dinâmico (TeVirtualMIDI)...");
        _cancellation = new CancellationTokenSource();

        CheckAndInitializePhysicalPorts();

        var ports = DatabaseManager.GetVirtualPorts();
        foreach (var p in ports)
        {
            if (CheckIfPortExists(p))
            {
                Log.Fatal($"=============================================================================");
                Log.Fatal($"ERRO FATAL: A porta {p} já existe no sistema!");
                Log.Fatal($"Certifique-se de que o loopMIDI original esteja fechado.");
                Log.Fatal($"=============================================================================");
                Environment.Exit(1);
                return;
            }
            CreateVirtualPortInternal(p);
        }

        ConnectPhysicalDevices();
    }

    private void CheckAndInitializePhysicalPorts()
    {
        var inputs = DatabaseManager.GetEnabledPhysicalPorts(true);
        var outputs = DatabaseManager.GetEnabledPhysicalPorts(false);

        // Se estiver vazio, popula automaticamente com CMCs compatíveis encontrados (Padrão inicial)
        if (inputs.Count == 0 && outputs.Count == 0)
        {
            Log.Information("Primeira execução: Auto-selecionando dispositivos compatíveis (CMC)...");
            var autoInputs = new List<string>();
            var autoOutputs = new List<string>();

            for (int i = 0; i < MidiIn.NumberOfDevices; i++)
            {
                try {
                    string name = MidiIn.DeviceInfo(i).ProductName;
                    if (IsCmcDevice(name)) autoInputs.Add(name);
                } catch { }
            }
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                try {
                    string name = MidiOut.DeviceInfo(i).ProductName;
                    if (IsCmcDevice(name)) autoOutputs.Add(name);
                } catch { }
            }

            if (autoInputs.Count > 0) DatabaseManager.SetEnabledPhysicalPorts(autoInputs, true);
            if (autoOutputs.Count > 0) DatabaseManager.SetEnabledPhysicalPorts(autoOutputs, false);
        }
    }

    private bool IsCmcDevice(string name)
    {
        return name.Contains("Steinberg CMC", StringComparison.OrdinalIgnoreCase) || 
               name.Contains("CMC-FD", StringComparison.OrdinalIgnoreCase) || 
               name.Contains("CMC-PD", StringComparison.OrdinalIgnoreCase);
    }

    private void CreateVirtualPortInternal(string portName)
    {
        try 
        {
            var vPort = new TeVirtualMIDI(portName);
            _virtualPorts[portName] = vPort;
            Log.Information($"Porta virtual '{portName}' criada no Windows com sucesso.");
            
            var token = _cancellation!.Token;
            _readTasks.Add(Task.Run(() => VirtualPortReadLoop(portName, vPort, token)));
            
            AutoRouteNewDevice(portName, isPhysicalInput: false, isPhysicalOutput: false, isVirtual: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Falha ao criar porta virtual {portName}.");
        }
    }

    public void CreateVirtualPort(string portName)
    {
        if (_virtualPorts.ContainsKey(portName)) return;
        DatabaseManager.AddVirtualPort(portName);
        CreateVirtualPortInternal(portName);
        DevicesRefreshed?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveVirtualPort(string portName)
    {
        DatabaseManager.RemoveVirtualPort(portName);
        if (_virtualPorts.TryGetValue(portName, out var vPort))
        {
            _virtualPorts.Remove(portName);
            try { vPort.shutdown(); } catch { }
        }
        DevicesRefreshed?.Invoke(this, EventArgs.Empty);
    }

    private bool CheckIfPortExists(string portName)
    {
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            try {
                if (MidiIn.DeviceInfo(i).ProductName.Contains(portName, StringComparison.OrdinalIgnoreCase))
                    return true;
            } catch { }
        }
        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            try {
                if (MidiOut.DeviceInfo(i).ProductName.Contains(portName, StringComparison.OrdinalIgnoreCase))
                    return true;
            } catch { }
        }
        return false;
    }

    private void ConnectPhysicalDevices()
    {
        ConnectedInputNames.Clear();
        ConnectedOutputNames.Clear();

        var enabledInputs = DatabaseManager.GetEnabledPhysicalPorts(true);
        var enabledOutputs = DatabaseManager.GetEnabledPhysicalPorts(false);

        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            try 
            {
                var info = MidiIn.DeviceInfo(i);
                if (enabledInputs.Contains(info.ProductName))
                {
                    try 
                    {
                        var midiIn = new MidiIn(i);
                        midiIn.MessageReceived += OnCmcInputMessageReceived;
                        midiIn.Start();
                        
                        if (!cmcInputs.ContainsKey(info.ProductName)) cmcInputs[info.ProductName] = new List<MidiIn>();
                        cmcInputs[info.ProductName].Add(midiIn);
                        _midiInNames[midiIn] = info.ProductName;
                        ConnectedInputNames.Add(info.ProductName);
                        Log.Information($"Input físico conectado: {info.ProductName}");
                        
                        AutoRouteNewDevice(info.ProductName, isPhysicalInput: true, isPhysicalOutput: false, isVirtual: false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Falha ao abrir porta de entrada MIDI: {info.ProductName}");
                    }
                }
            }
            catch { }
        }

        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            try
            {
                var info = MidiOut.DeviceInfo(i);
                if (enabledOutputs.Contains(info.ProductName))
                {
                    try 
                    {
                        var midiOut = new MidiOut(i);
                        if (!cmcOutputs.ContainsKey(info.ProductName)) cmcOutputs[info.ProductName] = new List<MidiOut>();
                        cmcOutputs[info.ProductName].Add(midiOut);
                        ConnectedOutputNames.Add(info.ProductName);
                        Log.Information($"Output físico conectado: {info.ProductName}");
                        
                        AutoRouteNewDevice(info.ProductName, isPhysicalInput: false, isPhysicalOutput: true, isVirtual: false);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Falha ao abrir porta de saída MIDI: {info.ProductName}");
                    }
                }
            }
            catch { }
        }
    }

    private void AutoRouteNewDevice(string deviceName, bool isPhysicalInput, bool isPhysicalOutput, bool isVirtual)
    {
        var allRoutes = DatabaseManager.GetRoutes();
        if (allRoutes.Any(r => r.Item1 == deviceName || r.Item2 == deviceName))
            return; // Já possui alguma rota salva, não sobrescreve a escolha do usuário

        var allVirtuals = DatabaseManager.GetVirtualPorts();

        if (isPhysicalInput)
        {
            foreach (var v in allVirtuals) DatabaseManager.AddRoute(deviceName, v);
        }
        if (isPhysicalOutput)
        {
            foreach (var v in allVirtuals) DatabaseManager.AddRoute(v, deviceName);
        }
        if (isVirtual)
        {
            foreach (var input in ConnectedInputNames) DatabaseManager.AddRoute(input, deviceName);
            foreach (var output in ConnectedOutputNames) DatabaseManager.AddRoute(deviceName, output);
        }
    }

    private void OnCmcInputMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        if (sender is not MidiIn midiIn) return;
        if (!_midiInNames.TryGetValue(midiIn, out string? sourceName)) return;

        var routes = DatabaseManager.GetRoutes().Where(r => r.Item1 == sourceName).Select(r => r.Item2).ToList();
        byte[] data = PackMidiMessage(e.RawMessage);

        foreach (var dest in routes)
        {
            if (IsMonitoring)
            {
                MessageRouted?.Invoke(this, new MidiMessageEventArgs
                {
                    SourcePort = sourceName,
                    DestPort = dest,
                    Data = data,
                    Timestamp = DateTime.Now
                });
            }

            if (_virtualPorts.TryGetValue(dest, out var vPort))
            {
                try { vPort.sendCommand(data); } catch { }
            }
            if (cmcOutputs.TryGetValue(dest, out var outList))
            {
                foreach (var o in outList)
                {
                    try { o.Send(e.RawMessage); } catch { }
                }
            }
        }
    }

    private byte[] PackMidiMessage(int rawMessage)
    {
        byte status = (byte)(rawMessage & 0xFF);
        int length = 1;
        if ((status >= 0x80 && status <= 0xBF) || (status >= 0xE0 && status <= 0xEF)) length = 3;
        else if (status >= 0xC0 && status <= 0xDF) length = 2;
        else if (status == 0xF1 || status == 0xF3) length = 2;
        else if (status == 0xF2) length = 3;

        byte[] data = new byte[length];
        data[0] = status;
        if (length > 1) data[1] = (byte)((rawMessage >> 8) & 0xFF);
        if (length > 2) data[2] = (byte)((rawMessage >> 16) & 0xFF);
        return data;
    }

    private void VirtualPortReadLoop(string portName, TeVirtualMIDI vPort, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                byte[] command = vPort.getCommand();
                if (command == null || command.Length == 0) continue;

                var routes = DatabaseManager.GetRoutes().Where(r => r.Item1 == portName).Select(r => r.Item2).ToList();
                
                int rawMsg = 0;
                if (command.Length <= 3)
                {
                    rawMsg = command[0];
                    if (command.Length > 1) rawMsg |= (command[1] << 8);
                    if (command.Length > 2) rawMsg |= (command[2] << 16);
                }

                foreach (var dest in routes)
                {
                    if (IsMonitoring)
                    {
                        MessageRouted?.Invoke(this, new MidiMessageEventArgs
                        {
                            SourcePort = portName,
                            DestPort = dest,
                            Data = command,
                            Timestamp = DateTime.Now
                        });
                    }

                    if (cmcOutputs.TryGetValue(dest, out var outList))
                    {
                        foreach (var o in outList)
                        {
                            if (command.Length <= 3)
                                try { o.Send(rawMsg); } catch { }
                            else if (command[0] == 0xF0) // SysEx
                                try { o.SendBuffer(command); } catch { }
                        }
                    }
                    if (_virtualPorts.TryGetValue(dest, out var vDest) && vDest != vPort)
                    {
                        try { vDest.sendCommand(command); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    Log.Error(ex, $"Erro na leitura da porta virtual {portName}.");
                break;
            }
        }
    }

    public void RefreshDevices()
    {
        Log.Information("Atualizando conexões MIDI devido a evento Hotplug...");
        
        foreach (var list in cmcInputs.Values)
        {
            foreach (var input in list) { input.Stop(); input.Dispose(); }
        }
        cmcInputs.Clear();
        _midiInNames.Clear();

        foreach (var list in cmcOutputs.Values)
        {
            foreach (var output in list) { output.Dispose(); }
        }
        cmcOutputs.Clear();

        ConnectPhysicalDevices();
        DevicesRefreshed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Log.Information("Fechando rotas MIDI e destruindo portas virtuais...");
        
        _cancellation?.Cancel();

        foreach (var vPort in _virtualPorts.Values)
        {
            try { vPort.shutdown(); } catch { }
        }
        _virtualPorts.Clear();

        foreach (var list in cmcInputs.Values)
        {
            foreach (var input in list) { input.Stop(); input.Dispose(); }
        }
        cmcInputs.Clear();
        _midiInNames.Clear();

        foreach (var list in cmcOutputs.Values)
        {
            foreach (var output in list) { output.Dispose(); }
        }
        cmcOutputs.Clear();
    }
}
