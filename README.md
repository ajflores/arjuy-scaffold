# arjuy-scaffold — MCP server (learning prototype, iteration 1)

Prototipo de MCP server en C#/.NET que mueve la generación determinística de código
boilerplate de los proyectos `arjuy*` (arjuySticker, arjuyBasquet, arjuyGym, arjuyTurismo,
etc.) desde un skill en lenguaje natural (probabilístico) a tools de un servidor MCP
(determinístico por construcción). El skill `backend-orchestrator` de cada proyecto sigue
existiendo como documentación del "por qué" de cada convención; este server es la
implementación mecánica del "cómo".

## Qué hace

Servidor MCP por **stdio transport** (`ModelContextProtocol` 2.2.0 + `Microsoft.Extensions.Hosting`
10.0.12, .NET 10) que expone 10 tools de generación de código C#. Ninguna tool escribe en disco
por defecto — todas devuelven el código generado como string; `generate_entity` y `generate_feature`
son las únicas con un parámetro opt-in (`outputFilePath` / `outputDirectoryPath`) para también
escribirlo a disco.

### Tools

| Tool | Qué genera | Estado |
|------|-----------|--------|
| `generate_entity` | Clase de entidad Domain que hereda `Entity` (o el nombre que le pases), con propiedades auto-implementadas | Completo |
| `generate_service` | Par `I{Name}Service` / `{Name}Service`, todo método devuelve `MResult<T>`, try/catch que convierte cualquier excepción en `MResult<T>.Fail(...)`, nunca deja escapar la excepción | Completo |
| `generate_repository` | Par `I{Name}Repository` / `{Name}Repository`, ambos extienden el genérico `IBaseRepository<T>`/`BaseRepository<T>` (CRUD heredado: `GetByIdAsync`, `GetAllAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ExistsAsync`), constructor solo reenvía el DbContext a `base(context)`, métodos custom opcionales como `Task`/`Task<T>` planos (sin `MResult<T>`, sin try/catch) | Completo |
| `generate_ef_configuration` | `Persistence.Database/Configurations/{Entity}Configuration.cs`, `IEntityTypeConfiguration<T>` con `ToTable`, `HasKey`, `HasQueryFilter(soft delete)` (omitido si `isLookup=true`), `Property(...)` por campo y relaciones `HasMany/HasOne` + `WithOne/WithMany` + `HasForeignKey` + `OnDelete` | Completo |
| `generate_dto` | `Business/Models/{Entity}Models.cs` con `{Entity}ResponseModel` y `{Entity}RequestModel`, propiedades planas, sin Data Annotations, strings con default `= string.Empty` | Completo |
| `generate_mapper` | Código de mapeo Entity ↔ DTO para **Mapperly** o **AutoMapper**, según el parámetro **`provider` (REQUERIDO, sin default)** — ver sección dedicada abajo | Completo |
| `generate_feature` | Tool compuesta: orquesta `generate_entity` + `generate_repository` + `generate_service` + `generate_ef_configuration` + `generate_dto` + `generate_mapper` para UNA entidad en una sola llamada, reutilizando las mismas funciones estáticas (nada duplicado) | Completo |
| `generate_middleware_config` | `Api/ConfigServices/{Name}Config.cs` como extension method de `IServiceCollection` (y opcionalmente de `WebApplication`) | Completo |
| `generate_di_registration` | `DependencyInjectionConfig.cs` con `RegisterAssembly(...)` por reflexión | **Stub/incompleto** — ver abajo |

### `generate_mapper` — por qué `provider` no tiene default

AutoMapper y Mapperly no son intercambiables por una razón de negocio, no solo técnica: **AutoMapper
requiere licencia paga para empresas grandes** (modelo de Eyrium desde 2024), mientras que
**Mapperly es MIT/gratuito** (source generator de Roslyn, sin costo de reflexión en runtime). Cuál
usar depende de la situación de licenciamiento del cliente destino — por eso `provider` es
`string` **obligatorio sin valor por defecto**: fuerza a quien llama (humano u orquestador) a
tomar esa decisión conscientemente en cada llamada, en vez de heredar un default silencioso que
podría ser la opción incorrecta (con costo de licencia) para ese cliente.

Ambas convenciones fueron **verificadas contra código real**, no inventadas:

- **Mapperly** — verificado contra `ArjuyTurismo.Business` (memoria previa de Engram): granularidad
  **por MÓDULO**, no por entidad — `[Mapper] public partial class {Modulo}Mapper` con métodos
  `partial` `To{Entity}Entity(...)` / `Update{Entity}Entity(...)` / `To{Entity}ResponseModel(...)`.
  Cuando la respuesta tiene campos derivados/calculados (no es un copy 1:1), el método deja de ser
  `partial` y pasa a tener cuerpo explícito con `return new {...}` (parámetro
  `hasDerivedResponseFields`).
- **AutoMapper** — verificado contra **`arjuyGym/ArjuyGymApp`** (código real, no genérico de
  manual): `ArjuyGymApp.Business/Mappings/{Entity}Profile.cs` (una clase `Profile` **por
  ENTIDAD**, no por módulo — `CategoryProfile`, `ClientProfile`, etc.) con `CreateMap<...>()` en el
  constructor; `ForMember(...)` solo aparece cuando el campo de destino no es un copy 1:1 (ver
  `ClientProfile`, que combina datos de `Client` y de su `User` relacionado). El registro es
  centralizado en `ArjuyGymApp.Api/ConfigServices/AutoMapperConfig.cs`:
  ```csharp
  public static class AutoMapperConfig
  {
      public static IServiceCollection AddServiceAutoMapper(this IServiceCollection services)
      {
          services.AddAutoMapper(_ => { }, typeof(ClientProfile));
          return services;
      }
  }
  ```
  AutoMapper 16 necesita **exactamente esa firma** (`AddAutoMapper(Action<IMapperConfigurationExpression>, params Type[])`)
  con un solo tipo "marcador" del ensamblado — **no** `AddAutoMapper(assembly)`. Por eso
  `generate_mapper`/`generate_feature` con `provider="automapper"` NO regeneran este registro en
  cada llamada — lo documentan como paso manual único por ensamblado en un comentario al final del
  archivo generado.

### `generate_feature` — qué NO automatiza

- No toca `AppDbContext` — agregar el `DbSet<{Entity}>` queda como paso manual (documentado en el
  resultado de la tool).
- No registra el mapper en DI — para AutoMapper es un paso único por ensamblado (ver arriba), para
  Mapperly no aplica (no requiere DI).
- No genera controller CRUD — queda para una próxima iteración.

## Convenciones cubiertas (de `arjuySticker/skills/backend-orchestrator`)

1. **Contrato de retorno `MResult<T>`** — cubierto por `generate_service` (factory methods
   `Success`/`Fail` asumidos como ya existentes en el proyecto Domain real; este scaffolder no
   los genera, solo los consume en el código generado).
2. **5 capas de Clean Architecture** (Domain, Persistence.Database, Repository, Business, Api) —
   documentado acá, no generado en su totalidad; el scaffolder produce código destinado a Domain
   (entidades), Repository (interfaces + implementaciones), Business (services) y
   Api/ConfigServices (config). Persistence.Database (EF configurations, `AppDbContext`,
   migraciones) queda **pendiente** para la próxima iteración.
3. **Prohibido `=>` en cuerpos de método** — respetado en todo el código generado por las 4 tools
   (bloques `{ }` con `return` explícito) y, en la medida de lo razonable, en el propio código del
   scaffolder (dogfooding). Los `=>` que sí aparecen en el código del servidor son lambdas de LINQ
   (`.Select(x => ...)`), no miembros con cuerpo de expresión — la regla del skill aplica a
   métodos, no a lambdas ni a propiedades autoimplementadas.
4. **`Api/ConfigServices/` centralizado** — cubierto por `generate_middleware_config`. Se aclara
   en la description de la tool que "middleware" acá significa "extension method de
   configuración de servicios/pipeline", no middleware clásico de ASP.NET Core.
5. **DI por reflexión (`RegisterAssembly`)** — cubierto parcialmente por `generate_di_registration`
   (ver limitaciones abajo).
6. **Entidades heredan de clase base con soft delete** — cubierto por `generate_entity`.
   La clase base real, ya existente en cada proyecto arjuy* en `Domain/Common/Entity.cs`,
   se llama `Entity` (no `BaseEntity`) y trae `Id: int`, `CreatedAt: DateTime` (default
   `ArgentinaTime.Now`, no `DateTime.UtcNow`), `UpdatedAt: DateTime?` y `Deleted: bool`.
   Esta tool NO genera esa clase base — se asume que ya existe en el proyecto destino — solo
   genera la entidad derivada con sus propiedades específicas, heredando de `Entity`; no hay
   que agregarle manualmente `Id`, `CreatedAt`, `UpdatedAt` ni `Deleted` a la lista de
   propiedades porque ya vienen heredadas. El nombre de la clase base es configurable vía el
   parámetro `baseEntityName` solo por si algún proyecto real usa otro nombre.

### Pendiente / incompleto para la próxima iteración

- `generate_di_registration` es un **stub**: siempre emite la misma implementación de referencia
  de `RegisterAssembly`, no inspecciona un ensamblado real ni valida un archivo existente. El
  parámetro `lifetime` solo cambia el método de registro (`AddScoped`/`AddTransient`/
  `AddSingleton`), no hay lógica real de escaneo en este proceso.
- `Persistence.Database` ya no es la única capa sin cobertura: `generate_ef_configuration` cubre
  las `IEntityTypeConfiguration<T>`, pero `AppDbContext` (el `DbSet<T>` por entidad) y las
  migraciones siguen sin generarse — `generate_feature` documenta el `DbSet` como paso manual
  pendiente en su resultado.
- `generate_repository` no infiere queries reales (`Include`, `Where`, `OrderBy`, etc.) para los
  métodos custom — solo genera la firma correcta (`Task`/`Task<T>`) con un cuerpo `TODO` que
  lanza `NotImplementedException`, igual que el placeholder de `generate_service`.
- `generate_ef_configuration` pluraliza el nombre de tabla por defecto agregando una `s` al final
  (`Category` → `Categorys`) — naive a propósito; para nombres irregulares pasar `tableName`
  explícito.
- `generate_dto` sigue el patrón `{Entity}ResponseModel`/`{Entity}RequestModel` (un solo modelo de
  request) documentado para esta tarea — nótese que el proyecto real `arjuyGym` en cambio separa
  `{Entity}CreateModel`/`{Entity}UpdateModel`; si el proyecto destino usa esa variante, ajustar el
  nombre de clase generado a mano (la forma/propiedades no cambian).
- No hay tests automatizados de las tools (solo smoke test manual vía stdio, ver abajo).
- `generate_entity` con `outputFilePath` (y `generate_feature` con `outputDirectoryPath`)
  sobrescriben el archivo sin pedir confirmación ni chequear si ya existe — comportamiento
  aceptable para un prototipo, pero a revisar antes de un uso real.
- `generate_feature` no genera controller CRUD ni toca `AppDbContext`/registro de DI del mapper —
  ver la sección dedicada arriba.

## Cómo compilar

```powershell
cd C:\Users\floaj\OneDrive\Documentos\Desarrollo\mcp\arjuy-scaffold
dotnet build
```

Build verificado sin errores ni warnings con .NET SDK 10.0.400, `TargetFramework=net10.0`, incluyendo
las 4 tools agregadas en la segunda iteración (`generate_ef_configuration`, `generate_dto`,
`generate_mapper`, `generate_feature`).

## Cómo probarlo manualmente (smoke test por stdio)

El protocolo MCP por stdio es JSON-RPC delimitado por líneas (newline-delimited), no framing
tipo Content-Length (LSP). Ejemplo (bash), pidiendo `tools/list`:

```bash
{ printf '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}\n{"jsonrpc":"2.0","method":"notifications/initialized"}\n{"jsonrpc":"2.0","id":2,"method":"tools/list"}\n'; sleep 3; } | dotnet bin/Debug/net10.0/arjuy-scaffold.dll
```

Importante: si cerrás stdin inmediatamente después de escribir los mensajes (por ejemplo con un
simple `printf ... | dotnet ...` sin el `sleep`), el servidor puede recibir EOF y empezar a
apagarse antes de terminar de escribir la respuesta en stdout — un cliente MCP real (Claude Code,
etc.) mantiene stdin abierto todo el tiempo de vida del proceso, así que esto solo afecta scripts
de prueba manuales, no el uso real.

## Cómo registrarlo en Claude Code

**No se modificó ningún `.mcp.json` real como parte de esta tarea** — esa decisión queda en manos
del usuario. Ejemplo de entrada para agregar manualmente a un `.mcp.json` (proyecto o usuario),
apuntando al proyecto fuente (recompila en cada arranque vía `dotnet run`):

```json
{
  "mcpServers": {
    "arjuy-scaffold": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\floaj\\OneDrive\\Documentos\\Desarrollo\\mcp\\arjuy-scaffold"
      ]
    }
  }
}
```

Alternativa apuntando directamente al `.dll` ya compilado (arranque más rápido, requiere
`dotnet build` previo y volver a buildear después de cada cambio):

```json
{
  "mcpServers": {
    "arjuy-scaffold": {
      "command": "dotnet",
      "args": [
        "C:\\Users\\floaj\\OneDrive\\Documentos\\Desarrollo\\mcp\\arjuy-scaffold\\bin\\Debug\\net10.0\\arjuy-scaffold.dll"
      ]
    }
  }
}
```

## Estructura del proyecto

```
arjuy-scaffold/
  Program.cs                          punto de entrada, host genérico + stdio transport
  Models/
    PropertyDefinition.cs             (Name, Type) para generate_entity / generate_dto
    ParameterDefinition.cs            (Name, Type) para parámetros de método
    MethodDefinition.cs               (Name, ReturnType, Parameters) para generate_service
    RepositoryMethodDefinition.cs     (Name, ReturnType, Parameters) para generate_repository
    EfPropertyDefinition.cs           (Name, IsRequired, MaxLength) para generate_ef_configuration
    EfRelationDefinition.cs           (PropertyName, RelationType, TargetEntity, ...) para generate_ef_configuration
    FeaturePropertyDefinition.cs      (Name, Type, IsRequired, MaxLength) para generate_feature
  Tools/
    EntityTool.cs                     generate_entity
    ServiceTool.cs                    generate_service
    RepositoryTool.cs                 generate_repository
    EfConfigurationTool.cs            generate_ef_configuration
    DtoTool.cs                        generate_dto
    MapperTool.cs                     generate_mapper (mapperly | automapper)
    FeatureTool.cs                    generate_feature (compuesta, orquesta las 6 anteriores)
    MiddlewareConfigTool.cs           generate_middleware_config
    DependencyInjectionTool.cs        generate_di_registration (stub)
```
