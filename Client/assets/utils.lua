local utils = {}

utils.printTable = function(tbl, indent)
  indent = indent or 0
  local indentString = string.rep("  ", indent)

  for key, value in pairs(tbl) do
    if type(value) == "table" then
      print(indentString .. tostring(key) .. ":")
      utils.printTable(value, indent + 1)
    else
      print(indentString .. tostring(key) .. ": " .. tostring(value))
    end
  end
end

utils.getEnumRepresentation = function(columnMap, column)
  local enumValue = columnMap[column]
  if not enumValue then
    error("Invalid column name: " .. tostring(column))
  end
  return enumValue
end

utils.debounce = function(func, delay)
  local timeoutRef = nil

  return function(...)
    local args = { ... }
    if timeoutRef then
      clearTimeout(timeoutRef)
    end

    timeoutRef = setTimeout(function()
      func(table.unpack(args))
      timeoutRef = nil
    end, delay)
  end
end

utils.timeRemaining = function(endTime)
  local pattern = "(%d+)-(%d+)-(%d+)T(%d+):(%d+):(%d+)"
  local year, month, day, hour, min, sec = endTime:match(pattern)

  -- Manually calculate the target UTC timestamp
  local targetTime = os.time({
    year = tonumber(year),
    month = tonumber(month),
    day = tonumber(day),
    hour = tonumber(hour),
    min = tonumber(min),
    sec = tonumber(sec)
  })

  -- Get current UTC time
  local currentTime = os.time(os.date("!*t"))

  -- Calculate the difference
  local diff = targetTime - currentTime

  -- Format output based on time difference
  if diff <= 0 then
    return "Expired"
  elseif diff >= 86400 then
    return string.format("%d days remaining", math.floor(diff / 86400))
  elseif diff >= 3600 then
    return string.format("%d hours remaining", math.floor(diff / 3600))
  elseif diff >= 60 then
    return string.format("%d minutes remaining", math.floor(diff / 60))
  else
    return string.format("%d seconds remaining", diff)
  end
end

utils.shallowClone = function(originalList)
  local newList = {}
  for i, v in ipairs(originalList) do
    newList[i] = v
  end
  return newList
end


return utils
