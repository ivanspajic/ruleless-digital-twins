namespace Logic.TTComponentInterfaces {
    public interface IConfigurableParameter {
        public string Name { get; }

        public void UpdateConfigurableParameter(string configurableParameterName, object configurableParameterValue);
    }
}
