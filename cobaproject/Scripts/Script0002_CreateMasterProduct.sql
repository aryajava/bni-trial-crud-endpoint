IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('LOSCONSUMER') AND name = 'MASTER_PRODUCT')
BEGIN
    CREATE TABLE LOSCONSUMER.MASTER_PRODUCT (
        -- Primary Key
        ID              INT             NOT NULL IDENTITY(1,1) PRIMARY KEY,

        -- Business Columns
        TITLE           NVARCHAR(500)   NOT NULL,
        PRICE           DECIMAL(18,2)   NOT NULL,
        DESCRIPTION     NVARCHAR(MAX)   NULL,
        CATEGORY        NVARCHAR(200)   NULL,
        IMAGE           NVARCHAR(1000)  NULL,
        RATING_RATE     DECIMAL(5,2)    NULL,
        RATING_COUNT    INT             NULL,

        -- 6 Kolom Wajib
        IS_ACTIVE       BIT             NOT NULL DEFAULT 1,
        CREATED_AT      DATETIME        NOT NULL DEFAULT GETDATE(),
        CREATED_BY      NVARCHAR(100)   NOT NULL DEFAULT 'SYSTEM',
        UPDATED_AT      DATETIME        NULL,
        UPDATED_BY      NVARCHAR(100)   NULL,
        VERSION         INT             NOT NULL DEFAULT 1   -- Optimistic Concurrency
    );
END;