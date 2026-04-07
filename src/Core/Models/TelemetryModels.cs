namespace OmenSuperHub {
  internal sealed class TemperatureSensorReading {
    public string Name { get; set; }
    public float Celsius { get; set; }
  }

  internal sealed class BatteryTelemetry {
    public bool PowerOnline { get; set; }
    public bool Charging { get; set; }
    public bool Discharging { get; set; }
    public int DischargeRateMilliwatts { get; set; }
    public int ChargeRateMilliwatts { get; set; }
    public int RemainingCapacityMilliwattHours { get; set; }
    public int VoltageMillivolts { get; set; }
  }
}
