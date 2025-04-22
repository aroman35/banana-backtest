#dotnet clean
#dotnet build
#rm -rf publish
#dotnet publish -o publish
./publish/Banana.Backtest.MoexConverter.exe `
    -o D:/share/moex-sources/202404_fut_log.csv `
    -t D:/share/moex-sources/202404_fut_deal.csv `
    -d D:/share/market-data-storage `
    -l NoCompression `
    -c NoCompression
