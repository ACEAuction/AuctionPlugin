-- Migration Script: 2025-02-02-02-Add-Subject-To-MailItem
-- Purpose: Add a `subject` column to the `mail_item` table for storing the subject message.

ALTER TABLE mail_item
ADD COLUMN subject VARCHAR(255) NOT NULL COMMENT 'Subject of the mail item';
