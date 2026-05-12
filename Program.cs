using System.Threading;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using HidSharp;

const int VID = 0x3633;
const int PID = 0x0013;

var computer = new Computer {
	IsMotherboardEnabled = true,
	IsCpuEnabled = true,
	IsGpuEnabled = true,
	IsPsuEnabled = true,
	IsMemoryEnabled = false,
	IsNetworkEnabled = false,
	IsStorageEnabled = false,
	IsControllerEnabled = false,
};

var packet = CH140Packet.Create();

computer.Open();
computer.Accept(new UpdateVisitor());

var mode = 0;
var modes = new byte[] { 0x02, 0x03, 0x04 };
var stay = 0;

HidDevice? hidDevice = null;
HidStream? hidStream = null;

while (true) {
	try {
		if (hidDevice is null) {
			hidDevice = DeviceList.Local
			    .GetHidDevices(VID, PID)
			    .First();
		}

		if (hidStream is null) {
			hidStream = hidDevice.Open();
			hidStream.WriteTimeout = 1000;
		}

		if (++stay % 3 == 0) {
			mode = (mode + 1) % modes.Length;
		}

		CreatePacket();
		packet.SetMode(modes[mode]);
		packet.Checksum();
		Console.WriteLine(packet);
		hidStream.Write(packet.ToBytes());
		Thread.Sleep(2000);
	} catch (ThreadInterruptedException) {
		hidStream.Close();
		computer.Close();
		Environment.Exit(0);
	} catch (Exception) {
		Thread.Sleep(2000);
		continue;
	}

}

unsafe void CreatePacket() {
	foreach (var hardware in computer.Hardware) {
		var gpu = "";

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
				writeBigEndianU16(maxfan, &packet.fan_rpm);
				break;
			case HardwareType.Cpu:
				var clocksum = 0.0f;
				var clockcount = 0;
				foreach (var sensor in hardware.Sensors) {
					if (sensor.Name is "CPU Package") {
						if (sensor.SensorType is SensorType.Temperature) {
							Console.WriteLine($"{sensor.Name}: {sensor.Value}°C");
							if (sensor.Value is float f) {
								writeBigEndianFloat(f, &packet.cpu_temp);
							}
						} else if (sensor.SensorType is SensorType.Power) {
							Console.WriteLine($"{sensor.Name}: {sensor.Value}W");
							if (sensor.Value is float f) {
								writeBigEndianU16(f, &packet.cpu_power);
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
							writeBigEndianU8(f, &packet.cpu_load);
						}
					}
				}
				if (clockcount > 0) {
					var cpuclock = clocksum / clockcount;
					Console.WriteLine($"CPU Clock: {cpuclock}MHz");
					writeBigEndianU16(cpuclock, &packet.cpu_clock);
				}
				break;
			case HardwareType.GpuNvidia:
				gpu = "nvidia";
				foreach (var sensor in hardware.Sensors) {
					if ((sensor.Name is "GPU Package") && (sensor.SensorType is SensorType.Power)) {
						Console.WriteLine($"GPU Package: {sensor.Value}W");
						if (sensor.Value is float f) {
							writeBigEndianU16(f, &packet.gpu_power);
						}
					} else if (sensor.Name is "GPU Core") {
						if (sensor.SensorType is SensorType.Temperature) {
							Console.WriteLine($"GPU Core: {sensor.Value}°C");
							if (sensor.Value is float f) {
								writeBigEndianFloat(f, &packet.gpu_temp);
							}
						} else if (sensor.SensorType is SensorType.Clock) {
							Console.WriteLine($"GPU Core: {sensor.Value}MHz");
							if (sensor.Value is float f) {
								writeBigEndianU16(f, &packet.gpu_clock);
							}
						} else if (sensor.SensorType is SensorType.Load) {
							Console.WriteLine($"GPU Core: {sensor.Value}%");
							if (sensor.Value is float f) {
								writeBigEndianU8(f, &packet.gpu_load);
							}
						}
					}
				}
				break;
			case HardwareType.GpuAmd:
				if (gpu is ("nvidia")) break;
				gpu = "amd";
				foreach (var sensor in hardware.Sensors) {
					if ((sensor.Name is "GPU Package") && (sensor.SensorType is SensorType.Power)) {
						Console.WriteLine($"GPU Package: {sensor.Value}W");
						if (sensor.Value is float f) {
							writeBigEndianU16(f, &packet.gpu_power);
						}
					} else if (sensor.Name is "GPU Core") {
						if (sensor.SensorType is SensorType.Temperature) {
							Console.WriteLine($"GPU Core: {sensor.Value}°C");
							if (sensor.Value is float f) {
								writeBigEndianFloat(f, &packet.gpu_temp);
							}
						} else if (sensor.SensorType is SensorType.Clock) {
							Console.WriteLine($"GPU Core: {sensor.Value}MHz");
							if (sensor.Value is float f) {
								writeBigEndianU16(f, &packet.gpu_clock);
							}
						} else if (sensor.SensorType is SensorType.Load) {
							Console.WriteLine($"GPU Core: {sensor.Value}%");
							if (sensor.Value is float f) {
								writeBigEndianU8(f, &packet.gpu_load);
							}
						}
					}
				}
				break;
			case HardwareType.GpuIntel:
				if (gpu is ("nvidia" or "amd")) break;
				gpu = "intel";
				foreach (var sensor in hardware.Sensors) {
					if ((sensor.Name is "GPU Package") && (sensor.SensorType is SensorType.Power)) {
						Console.WriteLine($"GPU Package: {sensor.Value}W");
						if (sensor.Value is float f) {
							writeBigEndianU16(f, &packet.gpu_power);
						}
					} else if (sensor.Name is "GPU Core") {
						if (sensor.SensorType is SensorType.Temperature) {
							Console.WriteLine($"GPU Core: {sensor.Value}°C");
							if (sensor.Value is float f) {
								writeBigEndianFloat(f, &packet.gpu_temp);
							}
						} else if (sensor.SensorType is SensorType.Clock) {
							Console.WriteLine($"GPU Core: {sensor.Value}MHz");
							if (sensor.Value is float f) {
								writeBigEndianU16(f, &packet.gpu_clock);
							}
						} else if (sensor.SensorType is SensorType.Load) {
							Console.WriteLine($"GPU Core: {sensor.Value}%");
							if (sensor.Value is float f) {
								writeBigEndianU8(f, &packet.gpu_load);
							}
						}
					}
				}
				break;
			default:
				Console.WriteLine($"[{hardware.HardwareType}] {hardware.Name}");
				break;
		}
	}
}

unsafe void writeBigEndianFloat(float value, byte** ptr) {
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

unsafe void writeBigEndianU16(float value, byte** ptr) {
	ushort intValue = (ushort)value;
	ushort bigEndianValue = (ushort)(((intValue & (0x00FF)) << 8)
		| ((intValue & 0xFF00) >> 8));
	byte* bValue = (byte*)&bigEndianValue;
	for (int i = 0; i < 2; i++) {
		(*ptr)[i] = bValue[i];
	}
}

unsafe void writeBigEndianU8(float value, byte** ptr) {
	(*ptr)[0] = (byte)value;
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

	public void SetMode(byte mode) {
		this.mode[0] = mode;
	}

	public void Checksum() {
		this.ToBytes();
		byte sum = 0;
		for (int i=0x1; i<=0x27; ++i) {
			sum += arr[i];
		}
		this.checksum[0] = sum;
	}

	public readonly override string ToString() {
		return Convert.ToHexString(this.ToBytes());
	}

	public readonly byte[] ToBytes() {
		IntPtr ptr = Marshal.AllocHGlobal(64);
		try {
			Marshal.StructureToPtr(this, ptr, false);
			Marshal.Copy(ptr, arr, 0, 64);
			return arr;
		} finally {
			Marshal.FreeHGlobal(ptr);
		}
	}

	private static byte[] arr = new byte[64];
};
