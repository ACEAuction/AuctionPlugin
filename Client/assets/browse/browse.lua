local rx = require('rx')
local utils = require('utils')

local state = rx:CreateState({
  pageNumber = 1,
  hasNextPage = false,
  HandlePageNumberInput = function() end,
  HandleListingsSearch = function() end,
})

local onPageNumberInputChange = utils.debounce(function(evt)
  state.HandlePageNumberInput(tonumber(evt.Params.value))
end, 1000)

local onSearchChange = utils.debounce(function(evt)
  state.HandleListingsSearch(evt.Params.value)
end, 1000)

local BrowseTitle = function(state)
  return rx:Div({
    class = {
      ["browse-title"] = true,
    }
  }, {
    rx:H4("Browse Active Auctions")
  })
end

local BrowseSearch = function(state)
  return rx:Div({ class = "browse-search" }, {
    rx:Div("Search: "),
    rx:Input({
      type = "text",
      onChange = onSearchChange
    })
  })
end

local BrowsePagination = function(state)
  return rx:Div({ class = "browse-pagination" }, {
    rx:Input({
      type = "text",
      value = state.pageNumber,
      onChange = onPageNumberInputChange
    }),
    rx:Button({
      disabled = state.pageNumber <= 1,
      class = {
        ["browse-pagination-previous"] = true,
        ["primary"] = true
      },
      onClick = function() state.HandlePreviousPage() end
    }, "Previous"),
    rx:Button({
      disabled = not state.hasNextPage,
      class = {
        ["browse-pagination-next"] = true,
        ["primary"] = true
      },
      onClick = function() state.HandleNextPage() end
    }, "Next")
  })
end

local BrowseControls = function(state)
  return rx:Div({ class = "browse-controls" }, {
    BrowseSearch(state),
    BrowsePagination(state)
  })
end

local BrowseItem = function(item)
  local itemIcon = string.format(
    "dat://0x%08X?underlay=0x%08X&overlay=0x%08X&uieffect=%s",
    tonumber(item.ItemIconId),
    tonumber(item.ItemIconUnderlay),
    tonumber(item.ItemIconOverlay),
    "")

  local currencyIcon = string.format(
    "dat://0x%08X?underlay=0x%08X&overlay=0x%08X&uieffect=%s",
    tonumber(item.CurrencyIconId),
    tonumber(item.CurrencyIconUnderlay),
    tonumber(item.CurrencyIconOverlay),
    "")
  return rx:Tr({ class = "browse-list-item", key = item.Id }, {
    rx:Td({
      rx:Div({ class = "browse-list-item-name-container" }, {
        rx:Div({
          style = string.format("decorator: image( %s )", itemIcon),
          class = "browse-list-item-icon"
        }),
        rx:Div({ class = "browse-list-item-name" }, item.ItemName),
      })
    }),
    rx:Td("x" .. (item.StackSize or "1")),
    rx:Td(tostring(item.BuyoutPrice)),
    rx:Td(tostring(item.StartPrice)),
    rx:Td(tostring(item.SellerName)),
    rx:Td({
      rx:Div({ class = "browse-list-item-name-container" }, {
        rx:Div({
          style = string.format("decorator: image( %s )", currencyIcon),
          class = "browse-list-item-icon"
        }),
        rx:Div({ class = "browse-list-item-name" }, item.CurrencyName),
      })
    }),
    rx:Td(tostring(utils.timeRemaining(item.EndTime))),
  })
end

local BrowseList = function(state)
  return rx:Div({
    class = {
      ["browse-list"] = true
    }
  }, {
    rx:Table({ class = "browse-list-table" }, {
      rx:Thead({ class = "browse-list-header" }, {
        rx:Tr({
          rx:Td({
            class = "browse-list-header-item",
            onClick = function() state.UpdateSortState("name") end
          }, {
            rx:Div({ class = "browse-list-header-item-container" }, {
              rx:Div("Name"),
              rx:Img({
                sprite = state.sortNameDirection == 1
                    and "combo-up-arrow"
                    or "combo-down-arrow"
              }),
            })
          }),
          rx:Td({
            class = "browse-list-header-item",
            onClick = function() state.UpdateSortState("stackSize") end
          }, {
            rx:Div({ class = "browse-list-header-item-container" }, {
              rx:Div("Stack Size"),
              rx:Img({
                sprite = state.sortStackSizeDirection == 1
                    and "combo-up-arrow"
                    or "combo-down-arrow"
              }),
            })
          }),
          rx:Td({
            class = "browse-list-header-item",
            onClick = function() state.UpdateSortState("buyoutPrice") end
          }, {
            rx:Div({ class = "browse-list-header-item-container" }, {
              rx:Div("Buyout Price"),
              rx:Img({
                sprite = state.sortBuyoutPriceDirection == 1
                    and "combo-up-arrow"
                    or "combo-down-arrow"
              }),
            })
          }),
          rx:Td({
            class = "browse-list-header-item",
            onClick = function() state.UpdateSortState("startPrice") end
          }, {
            rx:Div({ class = "browse-list-header-item-container" }, {
              rx:Div("Start Price"),
              rx:Img({
                sprite = state.sortStartPriceDirection == 1
                    and "combo-up-arrow"
                    or "combo-down-arrow"
              }),
            })
          }),
          rx:Td({
            class = "browse-list-header-item",
            onClick = function() state.UpdateSortState("seller") end
          }, {
            rx:Div({ class = "browse-list-header-item-container" }, {
              rx:Div("Seller"),
              rx:Img({
                sprite = state.sortSellerDirection == 1
                    and "combo-up-arrow"
                    or "combo-down-arrow"
              }),
            })
          }),
          rx:Td({
            class = "browse-list-header-item",
            onClick = function() state.UpdateSortState("currency") end
          }, {
            rx:Div({ class = "browse-list-header-item-container" }, {
              rx:Div("Currency"),
              rx:Img({
                sprite = state.sortCurrencyDirection == 1
                    and "combo-up-arrow"
                    or "combo-down-arrow"
              }),
            })
          }),
          rx:Td({
            class = "browse-list-header-item",
            onClick = function() state.UpdateSortState("duration") end
          }, {
            rx:Div({ class = "browse-list-header-item-container" }, {
              rx:Div("Duration"),
              rx:Img({
                sprite = state.sortDuration == 1
                    and "combo-up-arrow"
                    or "combo-down-arrow"
              }),
            })
          }),
        })
      }),
      state.listings and rx:Tbody(function()
        local ret = {}
        for i, item in ipairs(state.listings) do
          table.insert(ret, BrowseItem(item))
        end
        return ret
      end)
    })
  })
end

local BrowseView = function(state)
  return rx:Div({
    class = "browse-container",
  }, {
    BrowseTitle(state),
    BrowseControls(state),
    BrowseList(state)
  })
end

document:Mount(function() return BrowseView(state) end, "#browse")
