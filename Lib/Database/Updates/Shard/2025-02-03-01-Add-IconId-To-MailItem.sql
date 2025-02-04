-- Migration Script: 2025-02-03-01-Add-IconId-To-MailItem
-- Purpose: Add an `icon_id` column to the `mail_item` table for storing the icon identifier associated with the mail item.

ALTER TABLE mail_item
ADD COLUMN `icon_id` INT UNSIGNED NOT NULL COMMENT 'Icon identifier for the mail item' AFTER `item_id`;
