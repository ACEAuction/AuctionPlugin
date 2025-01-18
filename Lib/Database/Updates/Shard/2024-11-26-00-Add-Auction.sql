CREATE TABLE auction_sell_order (
    `id` INT UNSIGNED AUTO_INCREMENT NOT NULL COMMENT 'Unique Id for a sell order',
    `seller_id` INT UNSIGNED NOT NULL COMMENT 'Seller Id associated with the sell order',
    PRIMARY KEY (`id`)
);

CREATE TABLE auction_listing (
    `id` INT UNSIGNED AUTO_INCREMENT NOT NULL COMMENT 'Unique Id for an auctioned item',
    `item_id` INT UNSIGNED NOT NULL,
    `sell_order_id` INT UNSIGNED NOT NULL COMMENT 'Reference to AuctionSellOrder',
    `seller_id` INT UNSIGNED NOT NULL,
    `seller_name` VARCHAR(50) NOT NULL,
    `icon_id` INT UNSIGNED NOT NULL,
    `start_price` INT UNSIGNED NOT NULL,
    `buyout_price` INT UNSIGNED NOT NULL,
    `stack_size` INT UNSIGNED NOT NULL,
    `number_of_stacks` INT UNSIGNED NOT NULL,
    `currency_name` VARCHAR(50) NOT NULL,
    `highest_bid_amount` INT UNSIGNED,
    `highest_bid_id` INT UNSIGNED,
    `highest_bidder_id` INT UNSIGNED,
    `status` ENUM('active', 'completed', 'cancelled') DEFAULT 'active',
    `start_time` DATETIME NOT NULL,
    `end_time` DATETIME NOT NULL,
    PRIMARY KEY (`id`),
    FOREIGN KEY (`sell_order_id`) REFERENCES `auction_sell_order`(`id`)
);

CREATE INDEX idx_auction_listing_status ON auction_listing (status);

CREATE INDEX idx_auction_listing_status_endtime ON auction_listing (status, end_time);

CREATE TABLE auction_bid (
    `id` INT UNSIGNED AUTO_INCREMENT NOT NULL COMMENT 'Unique Id for an auction bid',
    `bidder_id` INT UNSIGNED NOT NULL,
	`bidder_name` VARCHAR(50) NOT NULL,
    `auction_listing_id` INT UNSIGNED NOT NULL,
    `bid_amount` INT UNSIGNED DEFAULT 0 NOT NULL,  
    `bid_time` DATETIME NOT NULL,
		`resolved` BOOL NOT NULL DEFAULT FALSE,
    PRIMARY KEY (`id`),
    FOREIGN KEY (`auction_listing_id`) REFERENCES `auction_listing`(`id`) 
);

CREATE TABLE auction_bid_item (
    `id` INT UNSIGNED AUTO_INCREMENT NOT NULL COMMENT 'Unique Id for an auction bid item used to purcahse an auction item',
    `bid_id` INT UNSIGNED NOT NULL,
    `item_id` INT UNSIGNED NOT NULL,
    PRIMARY KEY (`id`),
    FOREIGN KEY (`bid_id`) REFERENCES `auction_bid`(`id`) 
);

CREATE TABLE mail_item (
    `id` INT UNSIGNED AUTO_INCREMENT NOT NULL COMMENT 'Unique Id for an auction payout item.',
		`from` VARCHAR(50) NOT NULL,
    `item_id` INT UNSIGNED NOT NULL,
		`receiver_id` INT UNSIGNED NOT NULL,
    `status` ENUM('pending', 'sent', 'failed') DEFAULT 'pending',
    PRIMARY KEY (`id`)
);

CREATE INDEX idx_mail_item_receiver_status ON mail_item (receiver_id, status);

