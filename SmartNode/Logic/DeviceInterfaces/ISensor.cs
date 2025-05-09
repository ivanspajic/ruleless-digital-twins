using System.Numerics;

namespace Logic.DeviceInterfaces
{
    // Having a base interface without the generic type simplifies handling these types in collections.
    public interface ISensor
    {
        public string Name { get; init; }
    }

    public interface ISensor<T> : ISensor where T : INumber<T>
    {
        /// <summary>
        /// Observes the relevant property's value.
        /// </summary>
        /// <returns></returns>
        /// 
        /// This is just one way of handling observation. We could also consider a publish-subscribe pattern.
        public T ObservePropertyValue();
    }
}
