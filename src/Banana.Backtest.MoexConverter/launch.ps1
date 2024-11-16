dotnet clean
dotnet build
rm -rf publish
dotnet publish -o publish
./publish/Banana.Backtest.MoexConverter.exe `
    -o E:/market-data/moex/source/202404_fut_log/202404_fut_log.csv `
    -t E:/market-data/moex/source/202404_fut_deal/202404_fut_deal.csv `
    -d D:/market-data-storage `
    -l NoCompression `
    -c NoCompression
