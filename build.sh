#!/bin/bash
echo "🌸 Building Vue frontend..."
cd client && yarn build

echo "🌸 Copying dist to .NET wwwroot..."
rm -rf ../server/wwwroot/*
cp -r dist/* ../server/wwwroot/

echo "🌊 Building Docker image..."
cd ../server && docker build -t ecommerce-api .

echo "✅ Done. Run with: docker run -p 8080:80 ecommerce-api"