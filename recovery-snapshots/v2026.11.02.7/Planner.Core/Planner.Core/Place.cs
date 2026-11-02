namespace Planner.Core;

public sealed class Place : Entity
{
	public string InputName { get; set; } = "";

	public string Name { get; set; } = "";

	public string? Address { get; set; }

	public double? Lng { get; set; }

	public double? Lat { get; set; }

	public string? CoordType { get; set; }

	public string? ProviderId { get; set; }

	public string? Adcode { get; set; }

	public string Status { get; set; } = "UNVERIFIED";

	public string? Source { get; set; }

	public override string ToString()
	{
		return Name + (string.IsNullOrWhiteSpace(Address) ? "" : (" · " + Address));
	}
}
