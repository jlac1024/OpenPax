namespace OpenPax.Lua.Runtime;

/// <summary>A compiled statement: runs it in a frame and says how it ended.</summary>
internal delegate Signal Stat(Frame frame);

/// <summary>A compiled expression giving one value.</summary>
internal delegate LuaValue Expr(Frame frame);

/// <summary>A compiled expression giving any number of values (a call, ...).</summary>
internal delegate LuaValue[] MultiExpr(Frame frame);
