-- Copyright (c) .NET Foundation. All rights reserved.
-- Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

SET NOCOUNT ON
GO

USE master
GO
if exists (select * from sysdatabases where name='SqlServerReverseEngineerTestE2E')
    DROP DATABASE SqlServerReverseEngineerTestE2E
GO

DECLARE @device_directory NVARCHAR(520)
SELECT @device_directory = SUBSTRING(filename, 1, CHARINDEX(N'master.mdf', LOWER(filename)) - 1)
FROM master.dbo.sysaltfiles WHERE dbid = 1 AND fileid = 1

EXECUTE (N'CREATE DATABASE SqlServerReverseEngineerTestE2E
  ON PRIMARY (NAME = N''SqlServerReverseEngineerTestE2E'', FILENAME = N''' + @device_directory + N'SqlServerReverseEngineerTestE2E.mdf'')
  LOG ON (NAME = N''SqlServerReverseEngineerTestE2E_log'',  FILENAME = N''' + @device_directory + N'SqlServerReverseEngineerTestE2E_log.ldf'')')
GO

SET QUOTED_IDENTIFIER ON
GO

/* Set DATEFORMAT so that the date strings are interpreted correctly regardless of
   the default DATEFORMAT on the server.
*/
SET DATEFORMAT mdy
GO

SET ANSI_NULLS ON
GO

USE "SqlServerReverseEngineerTestE2E"
GO

if exists (select * from sysobjects where id = object_id('dbo.AllDataTypes') and sysstat & 0xf = 3)
	DROP TABLE "dbo"."AllDataTypes"
GO

if exists (select * from systypes where name = 'TestTypeAlias')
	drop TYPE "dbo"."TestTypeAlias"
GO

if exists (select * from sysobjects where id = object_id('dbo.PropertyConfiguration') and sysstat & 0xf = 3)
	DROP TABLE "dbo"."PropertyConfiguration"
GO

if exists (select * from sysobjects where id = object_id('[dbo].[Test Spaces Keywords Table]') and sysstat & 0xf = 3)
	DROP TABLE "dbo"."Test Spaces Keywords Table"
GO

if exists (select * from sysobjects where id = object_id('dbo.OneToManyDependent') and sysstat & 0xf = 3)
	drop table "dbo"."OneToManyDependent"
GO

if exists (select * from sysobjects where id = object_id('dbo.OneToManyPrincipal') and sysstat & 0xf = 3)
	drop table "dbo"."OneToManyPrincipal"
GO

if exists (select * from sysobjects where id = object_id('dbo.OneToOneDependent') and sysstat & 0xf = 3)
	drop table "dbo"."OneToOneDependent"
GO

if exists (select * from sysobjects where id = object_id('dbo.OneToOnePrincipal') and sysstat & 0xf = 3)
	drop table "dbo"."OneToOnePrincipal"
GO

if exists (select * from sysobjects where id = object_id('dbo.OneToOneSeparateFKDependent') and sysstat & 0xf = 3)
	drop table "dbo"."OneToOneSeparateFKDependent"
GO

if exists (select * from sysobjects where id = object_id('dbo.OneToOneSeparateFKPrincipal') and sysstat & 0xf = 3)
	drop table "dbo"."OneToOneSeparateFKPrincipal"
GO

if exists (select * from sysobjects where id = object_id('dbo.OneToOneFKToUniqueKeyDependent') and sysstat & 0xf = 3)
	drop table "dbo"."OneToOneFKToUniqueKeyDependent"
GO

if exists (select * from sysobjects where id = object_id('dbo.OneToOneFKToUniqueKeyPrincipal') and sysstat & 0xf = 3)
	drop table "dbo"."OneToOneFKToUniqueKeyPrincipal"
GO

if exists (select * from sysobjects where id = object_id('dbo.TableWithUnmappablePrimaryKeyColumn') and sysstat & 0xf = 3)
	drop table "dbo"."TableWithUnmappablePrimaryKeyColumn"
GO

if exists (select * from sysobjects where id = object_id('dbo.UnmappablePKColumn') and sysstat & 0xf = 3)
	drop table "dbo"."UnmappablePKColumn"
GO

-- TODO: Remove this
if exists (select * from sysobjects where id = object_id('dbo.ReferredToByTableWithUnmappablePrimaryKeyColumn') and sysstat & 0xf = 3)
	drop table "dbo"."ReferredToByTableWithUnmappablePrimaryKeyColumn"
GO

if exists (select * from sysobjects where id = object_id('dbo.SelfReferencing') and sysstat & 0xf = 3)
	drop table "dbo"."SelfReferencing"
GO

if exists (select * from sysobjects where id = object_id('dbo.MultipleFKsDependent') and sysstat & 0xf = 3)
	drop table "dbo"."MultipleFKsDependent"
GO

if exists (select * from sysobjects where id = object_id('dbo.MultipleFKsPrincipal') and sysstat & 0xf = 3)
	drop table "dbo"."MultipleFKsPrincipal"
GO

if exists (select * from sysobjects where id = object_id('dbo.FilteredOut') and sysstat & 0xf = 3)
	drop table "dbo"."FilteredOut"
GO

if exists (select * from sysobjects where id = object_id('dbo.PrimaryKeyWithSequence') and sysstat & 0xf = 3)
	drop table "dbo"."PrimaryKeyWithSequence"
GO

CREATE TYPE TestTypeAlias FROM nvarchar(max)
GO

CREATE TABLE "dbo"."AllDataTypes" (
	AllDataTypesID int IDENTITY(1, 1) PRIMARY KEY,
	bigintColumn bigint NOT NULL,
	bitColumn bit NOT NULL,
	decimalColumn decimal NOT NULL,
	intColumn int NOT NULL,
	moneyColumn money NOT NULL,
	numericColumn numeric NOT NULL,
	smallintColumn smallint NOT NULL,
	smallmoneyColumn smallmoney NOT NULL,
	tinyintColumn tinyint NOT NULL,
	floatColumn float NOT NULL,
	realColumn real NULL,
	dateColumn date NOT NULL,
	datetimeColumn datetime NULL,
	datetime2Column datetime2 NULL,
	datetime24Column datetime2(4) NULL,
	datetimeoffsetColumn datetimeoffset NULL,
	datetimeoffset5Column datetimeoffset(5) NULL,
	smalldatetimeColumn smalldatetime NULL,
	timeColumn time NULL,
	time4Column time(4) NULL,
	charColumn char NULL,
	char10Column char(10) NULL,
	textColumn text NULL,
	varcharColumn varchar NULL,
	varchar66Column varchar(66) NULL,
	varcharMaxColumn varchar(max) NULL,
	ncharColumn nchar NULL,
	nchar99Column nchar(99) NULL,
	ntextColumn ntext NULL,
	nvarcharColumn nvarchar NULL,
	nvarchar100Column nvarchar(100) NULL,
	nvarcharMaxColumn nvarchar(max) NULL,
	binaryColumn binary NULL,
	binary111Column binary(111) NULL,
	imageColumn image NULL,
	varbinaryColumn varbinary NULL,
	varbinary123Column varbinary(123) NULL,
	varbinaryMaxColumn varbinary(max) NULL,
	timestampColumn timestamp NULL,
	uniqueidentifierColumn uniqueidentifier NULL,
	hierarchyidColumn hierarchyid NULL,
	sql_variantColumn sql_variant NULL,
	xmlColumn xml NULL,
	geographyColumn geography NULL,
	geometryColumn geometry NULL,
	typeAliasColumn TestTypeAlias NULL,
	binaryVaryingColumn binary varying NULL,
	binaryVarying133Column binary varying(133) NULL,
	binaryVaryingMaxColumn binary varying(max) NULL,
	charVaryingColumn char varying NULL,
	charVarying144Column char varying(144) NULL,
	charVaryingMaxColumn char varying(max) NULL,
	characterColumn character NULL,
	character155Column character(155) NULL,
	characterVaryingColumn character varying NULL,
	characterVarying166Column character varying(166) NULL,
	characterVaryingMaxColumn character varying(max) NULL,
	nationalCharacterColumn national character NULL,
	nationalCharacter171Column national character(171) NULL,
	nationalCharVaryingColumn national char varying NULL,
	nationalCharVarying177Column national char varying(177) NULL,
	nationalCharVaryingMaxColumn national char varying(max) NULL,
	nationalCharacterVaryingColumn national char varying NULL,
	nationalCharacterVarying188Column national char varying(188) NULL,
	nationalCharacterVaryingMaxColumn national char varying(max) NULL,
)

GO

CREATE INDEX "IX_UnscaffoldableIndex"
	ON "dbo"."AllDataTypes" ( nvarcharColumn, sql_variantColumn, hierarchyidColumn )

GO

CREATE TABLE "dbo"."PropertyConfiguration" (
	"PropertyConfigurationID" "tinyint" IDENTITY(1, 1) PRIMARY KEY, -- tests error message about tinyint identity columns
	"WithDateDefaultExpression" "datetime2" NOT NULL DEFAULT (getdate()),
	"WithDateFixedDefault" "datetime2" NOT NULL DEFAULT ('October 20, 2015 11am'),
	"WithDateNullDefault" "datetime2" NULL DEFAULT (NULL),
	"WithGuidDefaultExpression" "uniqueidentifier" NOT NULL DEFAULT (newsequentialid()),
	"WithVarcharNullDefaultValue" "varchar" NULL DEFAULT (NULL),
	"WithDefaultValue" "int" NOT NULL DEFAULT ((-1)),
	"WithNullDefaultValue" "smallint" NULL DEFAULT (NULL),
	"WithMoneyDefaultValue" "money" NOT NULL DEFAULT ((0.00)),
	"A" "int" NOT NULL,
	"B" "int" NOT NULL,
	"SumOfAAndB" AS A + B PERSISTED, -- tests StoreGeneratedPattern
	"RowversionColumn" "rowversion" NOT NULL,
	"PropertyConfiguration" "int" NULL, -- tests column with same name as its table
	"ComputedDateTimeColumn" AS GETDATE()
)

GO

CREATE INDEX Test_PropertyConfiguration_Index
	ON "dbo"."PropertyConfiguration" (A, B)

GO

CREATE TABLE "dbo"."Test Spaces Keywords Table" (
	"Test Spaces Keywords TableID" "int" PRIMARY KEY,
	"abstract" "int" NOT NULL,
	"class" "int" NULL,
	"volatile" "int" NOT NULL,
	"Spaces In Column" "int" NULL,
	"Tabs	In	Column" "int" NOT NULL,
	"@AtSymbolAtStartOfColumn" "int" NULL,
	"@Multiple@At@Symbols@In@Column" "int" NOT NULL,
	"Commas,In,Column" "int" NULL,
	"$Dollar$Sign$Column" "int" NOT NULL,
	"!Exclamation!Mark!Column" "int" NULL,
	"""Double""Quotes""Column" "int" NULL,
	"\Backslashes\In\Column" "int" NULL,
)

GO

CREATE TABLE "SelfReferencing" (
	"SelfReferencingID" "int" PRIMARY KEY,
	"Name" nvarchar(20) NOT NULL,
	"Description" nvarchar(100) NOT NULL,
	"SelfReferenceFK" "int" NULL,
	CONSTRAINT "FK_SelfReferencing" FOREIGN KEY 
	(
		"SelfReferenceFK"
	) REFERENCES "dbo"."SelfReferencing" (
		"SelfReferencingID"
	)
)

GO

CREATE TABLE "OneToManyPrincipal" (
	"OneToManyPrincipalID1" "int",
	"OneToManyPrincipalID2" "int",
	"Other" nvarchar(20) NOT NULL,
	CONSTRAINT "PK_OneToManyPrincipal" PRIMARY KEY CLUSTERED 
	(
		"OneToManyPrincipalID1", "OneToManyPrincipalID2"
	)
)

GO

CREATE TABLE "OneToManyDependent" (
	"OneToManyDependentID1" "int",
	"OneToManyDependentID2" "int",
	"SomeDependentEndColumn" nvarchar (20) NOT NULL,
	"OneToManyDependentFK2" "int" NULL, -- deliberately put FK columns in other order to make sure we get correct order in key
	"OneToManyDependentFK1" "int" NULL,
	CONSTRAINT "PK_OneToManyDependent" PRIMARY KEY CLUSTERED 
	(
		"OneToManyDependentID1", "OneToManyDependentID2"
	),
	CONSTRAINT "FK_OneToManyDependent" FOREIGN KEY 
	(
		"OneToManyDependentFK1", "OneToManyDependentFK2"
	) REFERENCES "dbo"."OneToManyPrincipal" (
		"OneToManyPrincipalID1", "OneToManyPrincipalID2"
	)
)

GO

CREATE TABLE "OneToOnePrincipal" (
	"OneToOnePrincipalID1" "int",
	"OneToOnePrincipalID2" "int",
	"SomeOneToOnePrincipalColumn" nvarchar (20) NOT NULL,
	CONSTRAINT "PK_OneToOnePrincipal" PRIMARY KEY CLUSTERED 
	(
		"OneToOnePrincipalID1", "OneToOnePrincipalID2"
	)
)

GO

CREATE TABLE "OneToOneDependent" (
	"OneToOneDependentID1" "int",
	"OneToOneDependentID2" "int",
	"SomeDependentEndColumn" nvarchar (20) NOT NULL,
	CONSTRAINT "PK_OneToOneDependent" PRIMARY KEY CLUSTERED 
	(
		"OneToOneDependentID1", "OneToOneDependentID2"
	),
	CONSTRAINT "FK_OneToOneDependent" FOREIGN KEY 
	(
		"OneToOneDependentID1", "OneToOneDependentID2"
	) REFERENCES "dbo"."OneToOnePrincipal" (
		"OneToOnePrincipalID1", "OneToOnePrincipalID2"
	),
)

GO

CREATE TABLE "OneToOneSeparateFKPrincipal" (
	"OneToOneSeparateFKPrincipalID1" "int",
	"OneToOneSeparateFKPrincipalID2" "int",
	"SomeOneToOneSeparateFKPrincipalColumn" nvarchar (20) NOT NULL,
	CONSTRAINT "PK_OneToOneSeparateFKPrincipal" PRIMARY KEY CLUSTERED 
	(
		"OneToOneSeparateFKPrincipalID1", "OneToOneSeparateFKPrincipalID2"
	)
)

GO

CREATE TABLE "OneToOneSeparateFKDependent" (
	"OneToOneSeparateFKDependentID1" "int",
	"OneToOneSeparateFKDependentID2" "int",
	"SomeDependentEndColumn" nvarchar (20) NOT NULL,
	"OneToOneSeparateFKDependentFK1" "int" NULL,
	"OneToOneSeparateFKDependentFK2" "int" NULL,
	CONSTRAINT "PK_OneToOneSeparateFKDependent" PRIMARY KEY CLUSTERED 
	(
		"OneToOneSeparateFKDependentID1", "OneToOneSeparateFKDependentID2"
	),
	CONSTRAINT "FK_OneToOneSeparateFKDependent" FOREIGN KEY 
	(
		"OneToOneSeparateFKDependentFK1", "OneToOneSeparateFKDependentFK2"
	) REFERENCES "dbo"."OneToOneSeparateFKPrincipal" (
		"OneToOneSeparateFKPrincipalID1", "OneToOneSeparateFKPrincipalID2"
	),
	CONSTRAINT "UK_OneToOneSeparateFKDependent" UNIQUE
	(
		"OneToOneSeparateFKDependentFK1", "OneToOneSeparateFKDependentFK2"
	)
)

GO

CREATE TABLE "OneToOneFKToUniqueKeyPrincipal" (
	"OneToOneFKToUniqueKeyPrincipalID1" "int",
	"OneToOneFKToUniqueKeyPrincipalID2" "int",
	"SomePrincipalColumn" nvarchar (20) NOT NULL,
	"OneToOneFKToUniqueKeyPrincipalUniqueKey1" "int" NOT NULL,
	"OneToOneFKToUniqueKeyPrincipalUniqueKey2" "int" NOT NULL,
	CONSTRAINT "PK_OneToOneFKToUniqueKeyPrincipal" PRIMARY KEY CLUSTERED 
	(
		"OneToOneFKToUniqueKeyPrincipalID1", "OneToOneFKToUniqueKeyPrincipalID2"
	),
	CONSTRAINT "UK_OneToOneFKToUniqueKeyPrincipal" UNIQUE
	(
		"OneToOneFKToUniqueKeyPrincipalUniqueKey1", "OneToOneFKToUniqueKeyPrincipalUniqueKey2"
	)
)

GO

CREATE TABLE "OneToOneFKToUniqueKeyDependent" (
	"OneToOneFKToUniqueKeyDependentID1" "int",
	"OneToOneFKToUniqueKeyDependentID2" "int",
	"SomeColumn" nvarchar (20) NOT NULL,
	"OneToOneFKToUniqueKeyDependentFK1" "int" NULL,
	"OneToOneFKToUniqueKeyDependentFK2" "int" NULL,
	CONSTRAINT "PK_OneToOneFKToUniqueKeyDependent" PRIMARY KEY CLUSTERED 
	(
		"OneToOneFKToUniqueKeyDependentID1", "OneToOneFKToUniqueKeyDependentID2"
	),
	CONSTRAINT "FK_OneToOneFKToUniqueKeyDependent" FOREIGN KEY 
	(
		"OneToOneFKToUniqueKeyDependentFK1", "OneToOneFKToUniqueKeyDependentFK2"
	) REFERENCES "dbo"."OneToOneFKToUniqueKeyPrincipal" (
		"OneToOneFKToUniqueKeyPrincipalUniqueKey1", "OneToOneFKToUniqueKeyPrincipalUniqueKey2"
	),
	CONSTRAINT "UK_OneToOneFKToUniqueKeyDependent" UNIQUE
	(
		"OneToOneFKToUniqueKeyDependentFK1", "OneToOneFKToUniqueKeyDependentFK2"
	)
)

GO

CREATE TABLE "UnmappablePKColumn" (
	"UnmappablePKColumnID" "int" PRIMARY KEY,
	"AColumn" nvarchar(20) NOT NULL,
	"ValueGeneratedOnAddColumn" "int" IDENTITY(1, 1) NOT NULL,
)

GO

CREATE TABLE "TableWithUnmappablePrimaryKeyColumn" (
	"TableWithUnmappablePrimaryKeyColumnID" "hierarchyid" PRIMARY KEY,
	"AnotherColumn" nvarchar(20) NOT NULL,
	"TableWithUnmappablePrimaryKeyColumnFK" "int" NULL,
	CONSTRAINT "FK_TableWithUnmappablePrimaryKeyColumn" FOREIGN KEY 
	(
		"TableWithUnmappablePrimaryKeyColumnFK"
	) REFERENCES "dbo"."UnmappablePKColumn" (
		"UnmappablePKColumnID"
	),
	CONSTRAINT "UK_TableWithUnmappablePrimaryKeyColumn" UNIQUE
	(
		"AnotherColumn" -- tests that RevEng can assign an alternate key on a table with a PK which cannot be mapped
	)
)

GO

CREATE TABLE MultipleFKsPrincipal (
	MultipleFKsPrincipalId int PRIMARY KEY,
	SomePrincipalColumn nvarchar (20) NOT NULL
)

GO

CREATE TABLE MultipleFKsDependent (
	MultipleFKsDependentId int PRIMARY KEY,
	AnotherColumn nvarchar (20) NOT NULL,
	RelationAId int NOT NULL,
	RelationBId int NULL,
	RelationCId int NULL,
	CONSTRAINT FK_RelationA FOREIGN KEY 
	(
		RelationAId
	) REFERENCES dbo.MultipleFKsPrincipal (
		MultipleFKsPrincipalId
	),
	CONSTRAINT FK_RelationB FOREIGN KEY 
	(
		RelationBId
	) REFERENCES dbo.MultipleFKsPrincipal (
		MultipleFKsPrincipalId
	),
	CONSTRAINT FK_RelationC FOREIGN KEY 
	(
		RelationCId
	) REFERENCES dbo.MultipleFKsPrincipal (
		MultipleFKsPrincipalId
	)
)

GO
CREATE TABLE "FilteredOut" (
	"FilteredOutID" "int" PRIMARY KEY,
	"Unused1" nvarchar(20) NOT NULL,
	"Unused2" "int" NOT NULL,
)

GO
