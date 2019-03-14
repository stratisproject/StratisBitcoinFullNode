namespace Stratis.Bitcoin.NBitcoin.BouncyCastle.Math.EC.Endo
{
    internal interface ECEndomorphism
    {
        ECPointMap PointMap
        {
            get;
        }

        bool HasEfficientPointMap
        {
            get;
        }
    }
}
