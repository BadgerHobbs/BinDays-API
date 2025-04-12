namespace BinDays.Api.Initialisation
{
    using Autofac;
    using BinDays.Api.Collectors.Collectors;
    using BinDays.Api.Collectors.Services;

    /// <summary>
    /// Configures dependency injection for the application using Autofac.
    /// </summary>
    internal static class DependencyInjection
    {
        /// <summary>
        /// Configures the Autofac container with application-specific services.
        /// </summary>
        /// <param name="builder">The Autofac container builder.</param>
        public static void ConfigureContainer(ContainerBuilder builder)
        {
            // Register implementations of ICollector
            var collectorsAssembly = typeof(ICollector).Assembly;

            // Find types that are assignable to ICollector
            // Exclude the interface itself and any abstract base classes
            // Register them as ICollector
            builder.RegisterAssemblyTypes(collectorsAssembly)
                .AssignableTo<ICollector>()
                .Where(t => t.IsInterface == false && t.IsAbstract == false)
                .As<ICollector>();

            // Register collector service.
            builder.RegisterType<CollectorService>();
        }
    }
}
