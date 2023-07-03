
-- --------------------------------------------------
-- Entity Designer DDL Script for SQL Server 2005, 2008, 2012 and Azure
-- --------------------------------------------------
-- Date Created: 06/27/2023 03:53:43
-- Generated from EDMX file: C:\Users\Burak\Desktop\WebCrawler\Crawler\dbManager.edmx
-- --------------------------------------------------

SET QUOTED_IDENTIFIER OFF;
GO
USE [WebCrawler];
GO
IF SCHEMA_ID(N'dbo') IS NULL EXECUTE(N'CREATE SCHEMA [dbo]');
GO

-- --------------------------------------------------
-- Dropping existing FOREIGN KEY constraints
-- --------------------------------------------------


-- --------------------------------------------------
-- Dropping existing tables
-- --------------------------------------------------

IF OBJECT_ID(N'[dbo].[tblMainUrls]', 'U') IS NOT NULL
    DROP TABLE [dbo].[tblMainUrls];
GO

-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

-- Creating table 'tblMainUrls'
CREATE TABLE [dbo].[tblMainUrls] (
    [UrlHash] char(64)  NOT NULL,
    [Url] nvarchar(200)  NOT NULL,
    [DiscoverDate] datetime  NOT NULL,
    [LinkDepthLevel] smallint  NOT NULL,
    [ParentUrlHash] char(64)  NOT NULL,
    [LastCrawlingDate] datetime  NOT NULL,
    [SourceCode] varchar(max)  NULL,
    [FetchTimeMS] int  NOT NULL,
    [PageTile] nvarchar(max)  NULL,
    [CompressionPercent] tinyint  NOT NULL,
    [IsCrawled] bit  NOT NULL,
    [HostUrl] nvarchar(200)  NOT NULL,
    [CrawlTryCounter] tinyint  NOT NULL
);
GO

-- --------------------------------------------------
-- Creating all PRIMARY KEY constraints
-- --------------------------------------------------

-- Creating primary key on [UrlHash] in table 'tblMainUrls'
ALTER TABLE [dbo].[tblMainUrls]
ADD CONSTRAINT [PK_tblMainUrls]
    PRIMARY KEY CLUSTERED ([UrlHash] ASC);
GO

-- --------------------------------------------------
-- Creating all FOREIGN KEY constraints
-- --------------------------------------------------

-- --------------------------------------------------
-- Script has ended
-- --------------------------------------------------