namespace OpenPax.Sim.Galaxy;

/// <summary>
/// Strings the generator names things with, supplied by the caller from the
/// game's string lists so the simulation does not depend on file formats.
/// </summary>
/// <param name="StarNames">STR# 1000 (103 names; the generator only reaches the first 102).</param>
/// <param name="OrbitNumerals">STR# 111, the orbit numbers I to VI.</param>
/// <param name="JumpPrefix">STR# 110 entry 2, "Jump to ".</param>
public sealed record NameTables(IReadOnlyList<string> StarNames, IReadOnlyList<string> OrbitNumerals, string JumpPrefix);
