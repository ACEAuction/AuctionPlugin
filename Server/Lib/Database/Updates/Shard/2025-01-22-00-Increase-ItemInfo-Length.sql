-- Migration Script: 2025-01-22-00-Increase-ItemInfo-Length
-- Purpose: Increase the size of `item_info` column in `auction_listing` table

ALTER TABLE auction_listing 
MODIFY COLUMN item_info VARCHAR(500) NOT NULL;