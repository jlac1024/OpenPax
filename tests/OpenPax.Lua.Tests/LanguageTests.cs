using OpenPax.Lua.Runtime;

namespace OpenPax.Lua.Tests;

/// <summary>The language, script by script: each returns a value the test checks.</summary>
public sealed class LanguageTests
{
    private static LuaValue Run(string script)
    {
        var state = new LuaState();
        var values = state.DoString(script, "test.lua");
        return values.Length > 0 ? values[0] : LuaValue.Nil;
    }

    private static string Text(string script) => new LuaState() is var s ? s.ToDisplay(Run(script)) : "";

    [Theory]
    [InlineData("return 1 + 2", "3")]
    [InlineData("return 7 // 2, 7 % 3", "3")]
    [InlineData("return -7 // 2", "-4")]
    [InlineData("return -7 % 3", "2")]
    [InlineData("return 7 % -3", "-2")]
    [InlineData("return 5.5 % 2", "1.5")]
    [InlineData("return 1 / 2", "0.5")]
    [InlineData("return 3 / 1", "3.0")]
    [InlineData("return 2^10", "1024.0")]
    [InlineData("return 2^-1", "0.5")]
    [InlineData("return 9^0.5", "3.0")]
    [InlineData("return 10 / 3", "3.3333333333333")]
    [InlineData("return 1e15", "1e+15")]
    [InlineData("return 2^53", "9.007199254741e+15")]
    [InlineData("return math.maxinteger + 1 == math.mininteger", "true")]
    [InlineData("return 0x10 + 0xff", "271")]
    [InlineData("return 3 & 5 | 8 ~ 1", "9")]
    [InlineData("return 1 << 62 >> 61", "2")]
    [InlineData("return ~0", "-1")]
    [InlineData("return '10' + 5", "15")]
    [InlineData("return '3.5' * 2", "7.0")]
    [InlineData("return 1 == 1.0", "true")]
    [InlineData("return math.type(1), math.type(1.0)", "integer")]
    [InlineData("return math.floor(3.7), math.ceil(3.2)", "3")]
    [InlineData("return 2^63 == math.maxinteger + 1.0", "true")]
    [InlineData("return 1 < 1.5 and 2 > 1.5 and -1 <= -1.0", "true")]
    public void Numbers(string script, string expected) => Assert.Equal(expected, Text(script));

    [Theory]
    [InlineData("return 'a' .. 'b' .. 1 .. 2.0", "ab12.0")]
    [InlineData("return #'hello'", "5")]
    [InlineData("return ('x'):rep(3, '-')", "x-x-x")]
    [InlineData("return string.sub('hello', 2, -2)", "ell")]
    [InlineData("return ('Hello'):upper()", "HELLO")]
    [InlineData("return string.format('%5.2f|%-4d|%04d|%x|%s|%q', 3.14159, 7, 42, 255, 'hi', 'a\\nb')", " 3.14|7   |0042|ff|hi|\"a\\nb\"")]
    [InlineData("return string.format('%g %g %g', 100000, 1000000, 0.0001)", "100000 1e+06 0.0001")]
    [InlineData("return string.format('%e', 12345.678)", "1.234568e+04")]
    [InlineData("return string.find('hello world', 'o w')", "5")]
    [InlineData("return select(2, string.find('hello world', 'o w'))", "7")]
    [InlineData("return string.match('key = value', '(%w+)%s*=%s*(%w+)')", "key")]
    [InlineData("return select(2, string.match('key = value', '(%w+)%s*=%s*(%w+)'))", "value")]
    [InlineData("return (string.gsub('hello world', 'o', '0'))", "hell0 w0rld")]
    [InlineData("return (string.gsub('abc', '%w', '%0%0'))", "aabbcc")]
    [InlineData("return (string.gsub('$name is $age', '%$(%w+)', {name = 'Ann', age = 30}))", "Ann is 30")]
    [InlineData("return (string.gsub('a,b,,c', ',', ';', 2))", "a;b;,c")]
    [InlineData("local t = {} for w in string.gmatch('one two three', '%a+') do t[#t + 1] = w end return table.concat(t, '+')", "one+two+three")]
    [InlineData("return string.match('  trim  ', '^%s*(.-)%s*$')", "trim")]
    [InlineData("return string.match('f(a(b)c)d', '%b()')", "(a(b)c)")]
    [InlineData("return string.match('THE (quick) fox', '%f[%a]%a+', 5)", "quick")]
    [InlineData("return string.find('a.b', '.', 1, true)", "2")]
    [InlineData("return string.byte('A'), string.char(72, 105)", "65")]
    [InlineData("return tostring(nil) .. tostring(true)", "niltrue")]
    [InlineData("return tonumber('  0x1F  ') + tonumber('z', 36)", "66")]
    [InlineData("return tonumber('12abc')", "nil")]
    public void Strings(string script, string expected) => Assert.Equal(expected, Text(script));

    [Theory]
    [InlineData("local function f(n) if n <= 1 then return 1 end return n * f(n - 1) end return f(10)", "3628800")]
    [InlineData("local function counter() local n = 0 return function() n = n + 1 return n end end local c = counter() c() c() return c()", "3")]
    [InlineData("local fs = {} for i = 1, 3 do fs[i] = function() return i end end return fs[1]() + fs[2]() + fs[3]()", "6")]
    [InlineData("local function f(...) return select('#', ...) end return f(1, nil, 3)", "3")]
    [InlineData("local function f(...) local a, b = ... return b end return f(1, 2, 3)", "2")]
    [InlineData("local function f() return 1, 2, 3 end local t = {f()} return #t", "3")]
    [InlineData("local function f() return 1, 2, 3 end local t = {f(), 10} return #t", "2")]
    [InlineData("local function f() return 1, 2 end return (f())", "1")]
    [InlineData("local a, b, c = (function() return 1, 2 end)() return c", "nil")]
    [InlineData("local t = {} function t.add(a, b) return a + b end function t:twice(x) return self.add(x, x) end return t:twice(4)", "8")]
    [InlineData("local s = 0 for i = 10, 1, -3 do s = s + i end return s", "22")]
    [InlineData("local s = 0 for i = 1, 3 do for j = 1, 3 do if j == 2 then break end s = s + j end end return s", "3")]
    [InlineData("local s = 0 for x = 0, 1, 0.25 do s = s + x end return s", "2.5")]
    [InlineData("local n = 0 while true do n = n + 1 if n == 5 then break end end return n", "5")]
    [InlineData("local n = 0 repeat local k = n n = n + 1 until k >= 3 return n", "4")]
    [InlineData("local t = {10, 20, 30, x = 1} local s = 0 for i, v in ipairs(t) do s = s + i * v end return s", "140")]
    [InlineData("local a, b = 1, 2 a, b = b, a return a * 10 + b", "21")]
    [InlineData("x = 5 return x", "5")]
    [InlineData("local t = {} t.a = {} t.a.b = 3 return t.a.b", "3")]
    [InlineData("return nil or false or 'x'", "x")]
    [InlineData("return 1 and nil", "nil")]
    [InlineData("return not nil", "true")]
    [InlineData("local t = {1, 2, nil, 4} return select('#', table.unpack(t, 1, 4))", "4")]
    [InlineData("return #{n = 1, 1, 2, 3}", "3")]
    [InlineData("local x <const> = 4 return x", "4")]
    [InlineData("--[==[ long\ncomment ]==] return [[\nline]]", "line")]
    public void Functions(string script, string expected) => Assert.Equal(expected, Text(script));

    [Theory]
    [InlineData("local v = setmetatable({}, {__add = function(a, b) return 'added' end}) return v + 1", "added")]
    [InlineData("local base = {greet = function() return 'hi' end} local o = setmetatable({}, {__index = base}) return o.greet()", "hi")]
    [InlineData("local log = {} local o = setmetatable({}, {__newindex = function(t, k, v) rawset(t, k, v * 2) end}) o.x = 4 return o.x", "8")]
    [InlineData("local o = setmetatable({}, {__call = function(self, a) return a * 3 end}) return o(5)", "15")]
    [InlineData("local o = setmetatable({}, {__tostring = function() return 'thing' end}) return tostring(o)", "thing")]
    [InlineData("local o = setmetatable({}, {__len = function() return 42 end}) return #o", "42")]
    [InlineData("local mt = {__eq = function() return true end} return setmetatable({}, mt) == setmetatable({}, mt)", "true")]
    [InlineData("local mt = {__lt = function(a, b) return a.v < b.v end} return setmetatable({v = 1}, mt) < setmetatable({v = 2}, mt)", "true")]
    [InlineData("local mt = {__concat = function(a, b) return 'cat' end} return setmetatable({}, mt) .. 'x'", "cat")]
    [InlineData("local mt = {__index = function(t, k) return k .. '!' end} return setmetatable({}, mt).hey", "hey!")]
    [InlineData("return getmetatable(setmetatable({}, {__metatable = 'locked'}))", "locked")]
    public void Metatables(string script, string expected) => Assert.Equal(expected, Text(script));

    [Theory]
    [InlineData("local t = {} table.insert(t, 'a') table.insert(t, 1, 'b') table.insert(t, 'c') return table.concat(t, ',')", "b,a,c")]
    [InlineData("local t = {1, 2, 3} local r = table.remove(t, 1) return r .. ':' .. table.concat(t, ',')", "1:2,3")]
    [InlineData("local t = {5, 2, 8, 1} table.sort(t) return table.concat(t, ',')", "1,2,5,8")]
    [InlineData("local t = {5, 2, 8, 1} table.sort(t, function(a, b) return a > b end) return table.concat(t, ',')", "8,5,2,1")]
    [InlineData("local t = table.pack(1, nil, 3) return t.n", "3")]
    [InlineData("return math.max(3, 7.5, 2), math.min(4, 1)", "7.5")]
    [InlineData("return math.abs(-3), math.tointeger(4.0)", "3")]
    [InlineData("local ok, e = pcall(error, {code = 7}) return e.code", "7")]
    [InlineData("local ok, e = pcall(function() error('boom') end) return e", "test.lua:1: boom")]
    [InlineData("local ok, e = pcall(function() local x = nil return x.y end) return e", "test.lua:1: attempt to index a nil value (field 'y')")]
    [InlineData("local ok, e = pcall(function() undefined_function() end) return e", "test.lua:1: attempt to call a nil value (global 'undefined_function')")]
    [InlineData("local ok, e = pcall(function() return {} < {} end) return e", "test.lua:1: attempt to compare two table values")]
    [InlineData("local ok, e = pcall(function() return 1 // 0 end) return e", "test.lua:1: attempt to perform 'n//0'")]
    [InlineData("local ok, e = pcall(string.rep) return e", "test.lua:1: bad argument #1 to 'rep' (string expected, got no value)")]
    [InlineData("return select(-1, 'a', 'b', 'c')", "c")]
    [InlineData("return xpcall(function() error('x', 0) end, function(m) return 'handled ' .. m end)", "false")]
    [InlineData("return select(2, xpcall(function() error('x', 0) end, function(m) return 'handled ' .. m end))", "handled x")]
    public void Library(string script, string expected) => Assert.Equal(expected, Text(script));

    [Fact]
    public void PairsGoesInTheOrderKeysWereAdded()
    {
        var order = Text("""
            local t = {}
            t.zebra = 1 t.apple = 2 t[10] = 3 t.mango = 4 t[1] = 5 t[2] = 6
            t.apple = nil t.kiwi = 7
            local keys = {}
            for k in pairs(t) do keys[#keys + 1] = tostring(k) end
            return table.concat(keys, ' ')
            """);
        Assert.Equal("1 2 zebra 10 mango kiwi", order);
    }

    [Fact]
    public void ErrorsCarryTheirFileAndLine()
    {
        var e = Assert.Throws<LuaException>(() => new LuaState().DoString("local a = 1\nlocal b = 2\nreturn a + nil", "mod.lua"));
        Assert.Equal("mod.lua:3: attempt to perform arithmetic on a nil value", e.Message);

        var syntax = Assert.Throws<LuaException>(() => new LuaState().Load("local x = = 2", "bad.lua"));
        Assert.StartsWith("bad.lua:1:", syntax.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEndlessLoopRunsOutOfBudgetAndPcallCannotHideIt()
    {
        var state = new LuaState { Budget = 10_000 };
        Assert.Throws<LuaBudgetException>(() => state.DoString("while true do end"));
        Assert.Throws<LuaBudgetException>(() => state.DoString("while true do pcall(function() while true do end end) end"));

        // Each call from the host starts a fresh budget.
        Assert.Equal("ok", state.DoString("return 'ok'")[0].AsString);
    }

    [Fact]
    public void DeepRecursionIsAnErrorNotACrash()
    {
        var e = Assert.Throws<LuaException>(() => new LuaState().DoString("local function f() return f() + 1 end return f()"));
        Assert.Contains("stack overflow", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThereIsNoWayOut()
    {
        foreach (var name in new[] { "os", "io", "debug", "load", "loadstring", "dofile", "require", "collectgarbage", "package" })
        {
            Assert.Equal("nil", Text($"return type({name}) == 'nil' and 'nil' or 'present'"));
        }

        Assert.Equal("nil", Text("return math.sin"));
    }

    [Fact]
    public void RandomComesFromTheHost()
    {
        var state = new LuaState();
        Assert.Throws<LuaException>(() => state.DoString("return math.random()"));
        state.Random = new CountingRandom();
        Assert.Equal(3, state.DoString("return math.random(3, 9)")[0].AsInteger);
    }

    [Fact]
    public void PrintGoesToTheHost()
    {
        var lines = new List<string>();
        var state = new LuaState { Output = lines.Add };
        state.DoString("print('a', 1, nil, 2.5)");
        Assert.Equal(["a\t1\tnil\t2.5"], lines);
    }

    private sealed class CountingRandom : ILuaRandom
    {
        public long Next(long max) => 0;

        public double NextDouble() => 0.25;
    }
}
