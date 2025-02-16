#dotnet clean
#dotnet build
#rm -rf publish
#dotnet publish -o publish
./publish/Banana.Backtest.MoexConverter.exe `
    -o D:/FORTS_SOURCES/202409_fut_log.csv `
    -t D:/FORTS_SOURCES/202409_fut_deal.csv `
    -d D:/market-data-storage `
    -l NoCompression `
    -c NoCompression
