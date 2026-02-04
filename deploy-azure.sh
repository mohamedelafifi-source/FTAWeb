#!/bin/bash
# Deploy FTAWeb to Azure App Service (macOS/Linux)
# Run from project root: ./deploy-azure.sh

set -e

# 1. Publish the app (output to ./publish)
echo "Publishing..."
dotnet publish -c Release -o publish

# 2. Create zip from publish folder (fix: use ../app.zip so zip is in project root)
echo "Creating app.zip..."
cd publish
zip -r ../app.zip .
cd ..

# 3. Deploy to Azure (use newer command)
echo "Deploying to Azure..."
az webapp deploy \
  --resource-group FTApp-Resources \
  --name FTAppV1 \
  --src-path app.zip \
  --type zip

echo "Done. Remove local artifacts (optional): rm -rf publish app.zip"
