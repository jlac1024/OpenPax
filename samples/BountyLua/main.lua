-- Bounty, in Lua (docs/modding/lua.md): every enemy ship a player destroys
-- pays credits by its hull, now and then double, and the player is told.
-- The kills are counted in the game's store, so they outlast a save.
-- It does what the C# Bounty does (samples/Bounty), line for line.

local settings = mod.read_toml("settings.toml")
local per_hull = settings.credits_per_hull or 50
local double_chance = settings.double_chance or 0.1

mod.on_ship_destroyed(function(game, ship, owner, by)
  -- A ship lost to no one (a mine, say) or to its own side pays nothing.
  if by == nil or by == owner then
    return
  end

  -- The mod's own random numbers: the same in every copy of the game, and saved with it.
  local bonus = math.random() < double_chance
  local credits = per_hull * (ship.type + 1) * (bonus and 2 or 1)
  game:give_money(by, credits)

  local key = "kills." .. by.index
  local kills = (game.store:get(key) or 0) + 1
  game.store:set(key, kills)

  local text = bonus and "Bounty: %s credits, doubled! (%d kills)" or "Bounty: %s credits (%d kills)"
  game:tell(by, string.format(text, credits, kills))
end)

-- A save made before this version of the mod, or before it was turned on,
-- needs nothing done: the kill counts start from what the store holds.
mod.on_load(function(game, saved_version)
  if saved_version == nil then
    mod.log("joined a saved game; counting kills from here")
  end
end)
