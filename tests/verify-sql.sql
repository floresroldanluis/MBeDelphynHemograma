-- All writes in this script target temporary tables, never eDelphyn tables.
SET NOCOUNT ON;
SET XACT_ABORT ON;
CREATE TABLE #DONATION (ID_DONATION numeric(15,0) PRIMARY KEY, COD_DONATION varchar(20), DAT_DONATION datetime);
CREATE TABLE #DONATION_TEST (ID_DONATION numeric(15,0), ID_TEST numeric(15,0), DES_RESULT varchar(20), PRIMARY KEY(ID_DONATION,ID_TEST));
INSERT INTO #DONATION VALUES (1,'C000001','20261029'),(2,'C000002','20261029 23:59:59'),(3,'C000003','20261030');
INSERT INTO #DONATION_TEST VALUES (1,72,NULL),(1,93,'17'),(1,10,'NEG'),(2,72,'200'),(2,93,'14'),(3,93,'15');
DECLARE @fc varchar(20)='', @tc varchar(20)='', @fd datetime='20261029', @td datetime='20261030';
IF (SELECT COUNT(*) FROM #DONATION WHERE (@fc='' OR COD_DONATION>=@fc) AND (@tc='' OR COD_DONATION<=@tc) AND (@fd IS NULL OR DAT_DONATION>=@fd) AND (@td IS NULL OR DAT_DONATION<@td)) <> 2
    THROW 51000, 'FAIL: inclusive end date', 1;
PRINT 'PASS: date range includes entire final day and excludes next day.';
SET @fc='C000002'; SET @tc='C000003';
IF (SELECT COUNT(*) FROM #DONATION WHERE (@fc='' OR COD_DONATION>=@fc) AND (@tc='' OR COD_DONATION<=@tc) AND (@fd IS NULL OR DAT_DONATION>=@fd) AND (@td IS NULL OR DAT_DONATION<@td)) <> 1
    THROW 51000, 'FAIL: combined filters', 1;
PRINT 'PASS: donation and date filters intersect.';
BEGIN TRANSACTION;
DECLARE @expected int;
SELECT @expected=COUNT(*) FROM #DONATION_TEST WITH (UPDLOCK,HOLDLOCK) WHERE ID_TEST IN (72,73,74,75,76,77,78,92,93) AND ID_DONATION IN (1);
DELETE FROM #DONATION_TEST WHERE ID_TEST IN (72,73,74,75,76,77,78,92,93) AND ID_DONATION IN (1);
IF @@ROWCOUNT <> @expected THROW 51000, 'FAIL: affected row count', 1;
IF NOT EXISTS (SELECT 1 FROM #DONATION_TEST WHERE ID_DONATION=1 AND ID_TEST=10) THROW 51000, 'FAIL: unrelated test deleted', 1;
IF (SELECT COUNT(*) FROM #DONATION_TEST WHERE ID_DONATION IN (2,3)) <> 3 THROW 51000, 'FAIL: unselected donation changed', 1;
ROLLBACK TRANSACTION;
IF (SELECT COUNT(*) FROM #DONATION_TEST) <> 6 THROW 51000, 'FAIL: rollback', 1;
PRINT 'PASS: only selected hemogram rows deleted; other tests/donations preserved; rollback restores rows.';
PRINT 'SQL checks passed. No production data modified. These checks do not execute the application binary.';
