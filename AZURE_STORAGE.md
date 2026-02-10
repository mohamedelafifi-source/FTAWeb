# Azure Blob Storage for FTA Web

To use **the same data** when running locally and when deployed, store family data in Azure Blob Storage.

## 1. Create a Storage Account and container

1. In [Azure Portal](https://portal.azure.com), create a **Storage account** (or use an existing one).
2. Create a **container** in that storage account, e.g. `fta-families` (or leave the default name).
3. Under the storage account → **Access keys**, copy **Connection string** (key1 or key2).

## 2. Configure the app

**Local (same data as deployed):**

- In `appsettings.Development.json` (or User Secrets), set:
  - `FamilyStorage:Azure:ConnectionString` = your connection string
  - `FamilyStorage:Azure:ContainerName` = `fta-families` (or your container name)

**Deployed (Azure App Service):**

- In the App Service → **Configuration** → Application settings, add:
  - Name: `FamilyStorage__Azure__ConnectionString`
  - Value: your storage account connection string
- Optionally: `FamilyStorage__Azure__ContainerName` = `fta-families`

Use the **same** storage account and container for both local and deployed app so they share the same families, tree files, attachments, and passwords.

## 3. Behavior

- If `FamilyStorage:Azure:ConnectionString` is **set** (non-empty), the app uses **Azure Blob Storage** for all family data (tree files, attachments, password file).
- If it is **empty or missing**, the app uses **local/Azure VM file system** (current behavior: `App_Data/Families` or `HOME/data/Families`).

No code changes are needed when switching: only configuration.

## 4. Blob layout

- Tree files: `{familyName}/{fileName}.json`
- Attachments: `{familyName}/attachments/{personName}/{fileName}`
- Passwords: `_meta/family_passwords.txt`

The container is created automatically on first use if it does not exist.
