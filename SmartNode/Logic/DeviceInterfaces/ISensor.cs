using System.Numerics;

namespace Logic.DeviceInterfaces
{
    public interface ISensor<T> where T : INumber<T>
    {
        public string Name { get; init; }

        /// <summary>
        /// Observes the relevant property's value.
        /// </summary>
        /// <returns></returns>
        /// 
        /// This is just one way of handling observation. We could also consider a publish-subscribe pattern.
        public T ObservePropertyValue();
    }
}
