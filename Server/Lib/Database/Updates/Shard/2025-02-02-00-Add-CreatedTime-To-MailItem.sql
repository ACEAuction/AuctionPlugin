-- Migration Script: 2025-02-02-00-Add-CreatedTime-To-MailItem
-- Purpose: Add a `created_time` column to the `mail_item` table to track when a mail item was created.

ALTER TABLE mail_item
ADD COLUMN created_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Timestamp when the mail item was created';
