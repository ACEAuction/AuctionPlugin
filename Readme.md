## Commands

### Auction Item Tagging

In order to sell items in an auction, you need to create a listing of all your items. You do this by tagging the item.

- `/ah-tag inspect` This toggles your character into an inspect state, anything you appraise, it will attempt to tag to your auction listing.
- `/ah-tag list` This shows all of your currently tagged items and their item info.
- `/ah-tag add <guid>` This adds a world object if you provide its guid.
- `/ah-tag remove <guid>` This removes a world object from your tagged items list.
- `/ah-tag clear` This clears all of the world objects from your tagged items list.

### Auction Sell

This places a new auction listing, with all of your tagged items included

- `/ah-sell <currency_wcid> <amount of currency> <auction duration>` This requires the weenieClassId of the currency you are asking for, the amount of currency you want, and the duration in hours you want the auction to last. (if the mod is in dev mode, the duration is in seconds)

### Auction Bid

This places a bid on a current auction listing.

- `/ah-bid <auction id> <bid amount>` This requires the auction id to place a bid on, and the amount of currency you want to bid

### Auction List

- `/ah-list` This shows a list of currently active auction listings
- `/ah-list <listing id>` This accepts an optional listing id, to print detailed information of each item associated with this listing.
