using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using HidSharp;
using System.ServiceProcess;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

DeepCoolService service = new DeepCoolService();

if (Environment.UserInteractive) {
	CancellationTokenSource cts = new();
	await service.RunTimerLoopAsync(cts.Token);
} else {
	ServiceBase.Run(service);
}

class DeepCoolService : ServiceBase {
	const int VID = 0x3633;
	const int PID = 0x0013;
	const int INTERVAL_MS = 2000;
	const int STAY_COUNT = 3;

	readonly Computer _computer = new Computer {
		IsMotherboardEnabled = true,
		IsCpuEnabled = true,
		IsGpuEnabled = true,
		IsPsuEnabled = true,
		IsMemoryEnabled = false,
		IsNetworkEnabled = false,
		IsStorageEnabled = false,
		IsControllerEnabled = false,
	};
	CH140Packet _packet = CH140Packet.Create();
	byte[] _bytes = new byte[64];

	int _mode = 0;
	byte[] _modes = new byte[] { 0x02, 0x03, 0x04 };
	int _stay = 0;

	HidDevice? _hidDevice;
	HidStream? _hidStream;

	private readonly CancellationTokenSource _cts = new();
	private Task? _workerTask;

	protected override void OnStart(string[] args) {
		_workerTask = RunTimerLoopAsync(_cts.Token);
	}

	public async Task RunTimerLoopAsync(CancellationToken token) {
		_computer.Open();
		using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(INTERVAL_MS));
		try {
			while (await timer.WaitForNextTickAsync(token)) {
				DoWork();
			}
		} catch (OperationCanceledException) {
			// exit
		} finally {
			ResetDevice();
			_computer.Close();
		}
	}

	public void DoWork() {
		if (!EnsureDevice()) {
			return;
		}

		try {
			if (++_stay % STAY_COUNT == 0) {
				_mode = (_mode + 1) % _modes.Length;
			}

			CreatePacket();
			_packet.SetMode(_modes[_mode]);
			_packet.ToBytes(_bytes);
			var checksum = Checksum();
			_bytes[0x28] = checksum;
			_packet.SetChecksum(checksum);
			Console.WriteLine(_packet);
			_hidStream!.Write(_bytes);
		} catch (Exception) {
			ResetDevice();
		}
	}

	protected override void OnStop() {
		_cts.Cancel();
		try {
			_workerTask?.Wait(TimeSpan.FromSeconds(5));
		} catch {
		}
	}

	bool EnsureDevice() {
		if (_hidStream is not null) return true;
		try {
			_hidDevice = DeviceList.Local.GetHidDevices(VID, PID).First();
			_hidStream = _hidDevice.Open();
			_hidStream.WriteTimeout = 1000;
			return true;
		} catch {
			_hidDevice = null;
			_hidStream = null;
			return false;
		}
	}

	void ResetDevice() {
		try { _hidStream?.Close(); } catch { }
		_hidStream = null;
		_hidDevice = null;
	}

	unsafe void CreatePacket() {
		_computer.Accept(new UpdateVisitor());
		var gpu = "";
		foreach (var hardware in _computer.Hardware) {
			Console.WriteLine($"{hardware.HardwareType}: {hardware.Name}");
			var maxfan = 0f;
			switch (hardware.HardwareType) {
				case HardwareType.Motherboard:
					foreach (var sub in hardware.SubHardware) {
						if (sub.HardwareType is HardwareType.SuperIO) {
							Console.WriteLine($"{sub.HardwareType}: {sub.Name}");
							sub.Update();
							foreach (var sensor in sub.Sensors) {
								if (sensor.SensorType is SensorType.Fan) {
									if (sensor.Value is float f) {
										maxfan = (f > maxfan) ? f : maxfan;
									}
								}
							}
						}
					}
					Console.WriteLine($"CPU FAN: {maxfan}RPM");
					_packet.WriteFanRPM(maxfan);
					break;
				case HardwareType.Cpu:
					var clocksum = 0.0f;
					var clockcount = 0;
					foreach (var sensor in hardware.Sensors) {
						if (sensor.Name is "CPU Package") {
							if (sensor.SensorType is SensorType.Temperature) {
								Console.WriteLine($"{sensor.Name}: {sensor.Value}°C");
								if (sensor.Value is float f) {
									_packet.WriteCPUTemp(f);
								}
							} else if (sensor.SensorType is SensorType.Power) {
								Console.WriteLine($"{sensor.Name}: {sensor.Value}W");
								if (sensor.Value is float f) {
									_packet.WriteCPUPower(f);
								}
							}
						} else if (sensor.SensorType is SensorType.Clock) {
							if (sensor.Value is float f) {
								clocksum += f;
								clockcount++;
							}
						} else if ((sensor.Name is "CPU Total") && (sensor.SensorType is SensorType.Load)) {
							Console.WriteLine($"{sensor.Name}: {sensor.Value}%");
							if (sensor.Value is float f) {
								_packet.WriteCPULoad(f);
							}
						}
					}
					if (clockcount > 0) {
						var cpuclock = clocksum / clockcount;
						Console.WriteLine($"CPU Clock: {cpuclock}MHz");
						_packet.WriteCPUClock(cpuclock);
					}
					break;
				case HardwareType.GpuNvidia:
					gpu = "nvidia";
					WriteNvidiaGpu(hardware);
					break;
				case HardwareType.GpuAmd:
					if (gpu is ("nvidia")) break;
					gpu = "amd";
					WriteNvidiaGpu(hardware);
					break;
				case HardwareType.GpuIntel:
					if (gpu is ("nvidia" or "amd")) break;
					gpu = "intel";
					WriteNvidiaGpu(hardware);
					break;
				default:
					Console.WriteLine($"[{hardware.HardwareType}] {hardware.Name}");
					break;
			}
		}
	}

	unsafe void WriteNvidiaGpu(IHardware hardware) {
		foreach (var sensor in hardware.Sensors) {
			if ((sensor.Name is "GPU Package") && (sensor.SensorType is SensorType.Power)) {
				Console.WriteLine($"GPU Package: {sensor.Value}W");
				if (sensor.Value is float f) {
					_packet.WriteGPUPower(f);
				}
			} else if (sensor.Name is "GPU Core") {
				if (sensor.SensorType is SensorType.Temperature) {
					Console.WriteLine($"GPU Core: {sensor.Value}°C");
					if (sensor.Value is float f) {
						_packet.WriteGPUTemp(f);
					}
				} else if (sensor.SensorType is SensorType.Clock) {
					Console.WriteLine($"GPU Core: {sensor.Value}MHz");
					if (sensor.Value is float f) {
						_packet.WriteGPUClock(f);
					}
				} else if (sensor.SensorType is SensorType.Load) {
					Console.WriteLine($"GPU Core: {sensor.Value}%");
					if (sensor.Value is float f) {
						_packet.WriteGPULoad(f);
					}
				}
			}
		}
	}

	unsafe static void WriteBigEndianFloat(float value, byte** ptr) {
		uint intValue = *(uint*)&value;
		uint bigEndianValue = ((intValue & 0x000000FF) << 24)
			| ((intValue & 0x0000FF00) << 8)
			| ((intValue & 0x00FF0000) >> 8)
			| ((intValue & 0xFF000000) >> 24);
		byte* bValue = (byte*)&bigEndianValue;
		for (int i = 0; i < 4; i++) {
			(*ptr)[i] = bValue[i];
		}
	}

	unsafe static void WriteBigEndianU16(float value, byte** ptr) {
		ushort intValue = (ushort)value;
		ushort bigEndianValue = (ushort)(((intValue & (0x00FF)) << 8)
			| ((intValue & 0xFF00) >> 8));
		byte* bValue = (byte*)&bigEndianValue;
		for (int i = 0; i < 2; i++) {
			(*ptr)[i] = bValue[i];
		}
	}

	unsafe static void WriteBigEndianU8(float value, byte** ptr) {
		(*ptr)[0] = (byte)value;
	}

	byte Checksum() {
		byte sum = 0;
		for (int i = 0x1; i <= 0x27; ++i) {
			sum += _bytes[i];
		}
		return sum;
	}
}

public class UpdateVisitor : IVisitor {
	public void VisitComputer(IComputer computer) => computer.Traverse(this);

	public void VisitHardware(IHardware hardware) {
		hardware.Update();
		foreach (IHardware subHardware in hardware.SubHardware)
			subHardware.Accept(this);
	}

	public void VisitSensor(ISensor sensor) { }

	public void VisitParameter(IParameter parameter) { }
}

[StructLayout(LayoutKind.Explicit, Size = 64, Pack = 1)]
unsafe struct CH140Packet {
	[FieldOffset(0x00)] public fixed byte begin[1];
	[FieldOffset(0x01)] public fixed byte prefix[5];
	[FieldOffset(0x06)] public fixed byte mode[1];
	[FieldOffset(0x07)] public fixed byte cpu_power[2];
	[FieldOffset(0x0A)] public fixed byte cpu_temp[4];
	[FieldOffset(0x0E)] public fixed byte cpu_load[1];
	[FieldOffset(0x0F)] public fixed byte cpu_clock[2];
	[FieldOffset(0x11)] public fixed byte fan_rpm[2];
	[FieldOffset(0x13)] public fixed byte gpu_power[2];
	[FieldOffset(0x15)] public fixed byte gpu_temp[4];
	[FieldOffset(0x19)] public fixed byte gpu_load[1];
	[FieldOffset(0x1A)] public fixed byte gpu_clock[2];
	[FieldOffset(0x1C)] public fixed byte psu_used[2];
	[FieldOffset(0x1E)] public fixed byte psu_temp[4];
	[FieldOffset(0x22)] public fixed byte psu_load[1];
	[FieldOffset(0x23)] public fixed byte psu_total[2];
	[FieldOffset(0x25)] public fixed byte psu_fan[2];
	[FieldOffset(0x28)] public fixed byte checksum[1];
	[FieldOffset(0x29)] public fixed byte end[1];

	public static CH140Packet Create() {
		var p = new CH140Packet();
		p.begin[0] = 0x10;
		p.prefix[0] = 0x68;
		p.prefix[1] = 0x01;
		p.prefix[2] = 0x06;
		p.prefix[3] = 0x23;
		p.prefix[4] = 0x01;
		p.end[0] = 0x16;
		return p;
	}

	public void WriteCPUPower(float value) {
		fixed (byte* p = this.cpu_power) {
			WriteBigEndianU16(value, &p);
		}
	}

	public void WriteCPUTemp(float value) {
		fixed (byte* p = this.cpu_temp) {
			WriteBigEndianFloat(value, &p);
		}
	}

	public void WriteCPULoad(float value) {
		fixed (byte* p = this.cpu_load) {
			WriteBigEndianU8(value, &p);
		}
	}

	public void WriteCPUClock(float value) {
		fixed (byte* p = this.cpu_clock) {
			WriteBigEndianU16(value, &p);
		}
	}

	public void WriteFanRPM(float value) {
		fixed (byte* p = this.fan_rpm) {
			WriteBigEndianU16(value, &p);
		}
	}

	public void WriteGPUPower(float value) {
		fixed (byte* p = this.gpu_power) {
			WriteBigEndianU16(value, &p);
		}
	}

	public void WriteGPUTemp(float value) {
		fixed (byte* p = this.gpu_temp) {
			WriteBigEndianFloat(value, &p);
		}
	}

	public void WriteGPULoad(float value) {
		fixed (byte* p = this.gpu_load) {
			WriteBigEndianU8(value, &p);
		}
	}

	public void WriteGPUClock(float value) {
		fixed (byte* p = this.gpu_clock) {
			WriteBigEndianU16(value, &p);
		}
	}

	public void SetMode(byte m) => mode[0] = m;

	public void SetChecksum(byte m) => checksum[0] = m;

	public readonly override string ToString() {
		var arr = new byte[64];
		this.ToBytes(arr);
		return Convert.ToHexString(arr);
	}

	public readonly void ToBytes(byte[] arr) {
		IntPtr ptr = Marshal.AllocHGlobal(64);
		try {
			Marshal.StructureToPtr(this, ptr, false);
			Marshal.Copy(ptr, arr, 0, 64);
		} finally {
			Marshal.FreeHGlobal(ptr);
		}
	}

	unsafe static void WriteBigEndianFloat(float value, byte** ptr) {
		uint intValue = *(uint*)&value;
		uint bigEndianValue = ((intValue & 0x000000FF) << 24)
			| ((intValue & 0x0000FF00) << 8)
			| ((intValue & 0x00FF0000) >> 8)
			| ((intValue & 0xFF000000) >> 24);
		byte* bValue = (byte*)&bigEndianValue;
		for (int i = 0; i < 4; i++) {
			(*ptr)[i] = bValue[i];
		}
	}

	unsafe static void WriteBigEndianU16(float value, byte** ptr) {
		ushort intValue = (ushort)value;
		ushort bigEndianValue = (ushort)(((intValue & (0x00FF)) << 8)
			| ((intValue & 0xFF00) >> 8));
		byte* bValue = (byte*)&bigEndianValue;
		for (int i = 0; i < 2; i++) {
			(*ptr)[i] = bValue[i];
		}
	}

	unsafe static void WriteBigEndianU8(float value, byte** ptr) {
		(*ptr)[0] = (byte)value;
	}
};
