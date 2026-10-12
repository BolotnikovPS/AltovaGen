// AltovaGen — dependency-injection registration
using AltovaGen.Abstractions;
using AltovaGen.Compat;
using AltovaGen.DependencyInjection;
using AltovaGen.Engine.Bcl;
using AltovaGen.Engine.Saxon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AltovaGen.DependencyInjection;

/// <summary>
/// Registers AltovaGen with a .NET dependency-injection container.
/// </summary>
public static class AltovaGenServiceCollectionExtensions
{
    /// <summary>
    /// Adds the AltovaGen engines and the COM-free compatibility
    /// facades to the container, using the default engine set: BCL engines for
    /// XSLT 1.0 / XQuery 1.0 subset / XSD 1.0 and the bundled SaxonCS-HE engine for
    /// XSLT 2.0/3.0 (including the <c>altova:evaluate</c> extension bridge).
    /// </summary>
    /// <seealso cref="AddAltovaGen(IServiceCollection, Action{AltovaGenOptions})"/>
    public static IServiceCollection AddAltovaGen(this IServiceCollection services)
        => services.AddAltovaGen(_ => { });

    /// <summary>
    /// Adds the AltovaGen engines and the COM-free compatibility
    /// facades to the container, with optional engine overrides. This is the DI
    /// counterpart of <see cref="IEngineRouter.RegisterSaxonEngines" />: pass your own
    /// XSLT 2.0/3.0, XQuery 3.1 or XSD 1.1 engines here instead of calling the router
    /// after construction.
    /// <example>
    /// <code>
    /// services.AddAltovaGen(options =>
    /// {
    ///     options.SaxonXQuery = mySaxonXQueryEngine;
    ///     options.SaxonValidation = myXsd11Engine;
    /// });
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the optional engines.</param>
    public static IServiceCollection AddAltovaGen(
        this IServiceCollection services,
        Action<AltovaGenOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AltovaGenOptions();
        configure(options);

        // Engines: stateless scalar services shared by the whole application.
        services.TryAddSingleton<BclXsltEngine>();
        services.TryAddSingleton<BclXQueryEngine>();
        services.TryAddSingleton<BclValidatorEngine>();

        // Saxon engine: build it with the configured extension-function registry
        // (defaults to the bundled Altova evaluate() bridge).
        services.TryAddSingleton<IExtensionFunctionRegistry>(_ =>
            options.ExtensionFunctions ?? new SaxonExtensionFunctionRegistry());
        services.TryAddSingleton<SaxonXsltEngine>(sp =>
            new SaxonXsltEngine(sp.GetRequiredService<IExtensionFunctionRegistry>()));

        // Router: built from the container-registered engines so that the exact
        // instances registered in the container are the ones it selects.
        services.TryAddSingleton<IEngineRouter>(sp =>
        {
            var router = new EngineRouter(
                sp.GetRequiredService<BclXsltEngine>(),
                sp.GetRequiredService<BclXQueryEngine>(),
                sp.GetRequiredService<BclValidatorEngine>(),
                sp.GetRequiredService<SaxonXsltEngine>());

            router.RegisterSaxonEngines(
                options.SaxonXslt ?? sp.GetRequiredService<SaxonXsltEngine>(),
                options.SaxonXQuery,
                options.SaxonValidation);

            return router;
        });

        // Compatibility facades: stateful per-operation objects (one per use, like the
        // original Altova COM coclasses).
        services.TryAddTransient<IApplication, Application>();
        services.TryAddTransient<IXSLT1, XSLT1>();
        services.TryAddTransient<IXSLT2, XSLT2>();
        services.TryAddTransient<IXQuery, XQuery>();
        services.TryAddTransient<IXMLValidator, XMLValidator>();

        return services;
    }
}
