# arjuy-scaffold — MCP server (learning prototype, iteration 1)

Prototipo de MCP server en C#/.NET que mueve la generación determinística de código
boilerplate de los proyectos `arjuy*` (arjuySticker, arjuyBasquet, arjuyGym, arjuyTurismo,
etc.) desde un skill en lenguaje natural (probabilístico) a tools de un servidor MCP
(determinístico por construcción). El skill `backend-orchestrator` de cada proyecto sigue
existiendo como documentación del "por qué" de cada convención; este server es la
implementación mecánica del "cómo".

## Qué hace

Servidor MCP por **stdio transport** (`ModelContextProtocol` 2.2.0 + `Microsoft.Extensions.Hosting`
10.0.12, .NET 10) que expone 13 tools: generadores de código C# y verificación de convenciones.

## Reglas de convenciones (obligatorias)

1. **Identificadores en inglés.** Namespaces, tipos, miembros, parámetros, variables, valores de
   enum, entidades, propiedades y rutas de API van en inglés (`Edition`, `GetByIdAsync`,
   `/api/editions`). Los textos visibles al usuario (strings, mensajes) y el contenido de seed
   pueden estar en español.
2. **Comentarios en español.** Los comentarios `//` y los XML docs `///` se escriben en español
   (las plantillas y el código generado ya los traen en español). Es una advertencia no bloqueante
   del verificador (heurística de palabras inglesas comunes).
3. **Un tipo por archivo.** Cada tipo público (clase, interfaz, enum, record) vive en su propio
   archivo `.cs`, con el nombre del tipo, y el namespace sigue la carpeta.
4. **Usar los generadores, no escribir a mano.** Entidades, DTOs, repositorios, servicios,
   configuraciones EF y mappers se generan con estas tools; lo que el MCP no tenga se corrige en el
   MCP, no se reemplaza con tipos propios (si es inevitable: desvío registrado en `SLICES.md`).
5. **Verificar antes de cerrar** con `verify_conventions` o `python3 scripts/verify-conventions.py`.

## Salida de las tools: lista de archivos `{path, content}`

Todas las tools de generación devuelven una **lista de archivos** `{path, content}` (ruta relativa a
la raíz de la solución), **uno por tipo público**. Ninguna escribe en disco por defecto; el
parámetro opt-in `outputDirectoryPath` (raíz de la solución) escribe además los archivos
(`generate_entity` y `generate_service` conservan además `outputFilePath` / `interfaceOutputFilePath`
/ `classOutputFilePath`).

Ejemplo de `generate_feature(entityName="Edition", mapperProvider="mapperly", namespaceRoot="Demo")`:

```
Demo.Domain/Entities/Edition.cs
Demo.Repository/Interfaces/IEditionRepository.cs
Demo.Repository/Repositories/EditionRepository.cs
Demo.Business/Interfaces/IEditionService.cs
Demo.Business/Services/EditionService.cs                  (hereda BaseService<Edition, EditionResponseModel>)
Demo.Persistence.Database/Configurations/EditionConfiguration.cs
Demo.Business/Models/Editions/EditionResponseModel.cs     (carpeta en PLURAL por entidad)
Demo.Business/Models/Editions/EditionRequestModel.cs
Demo.Business/Mappers/EditionMapper.cs                    (un mapper por entidad)
```

- **Modelos (DTOs) en subcarpetas por entidad**, en plural para que el namespace no choque con el
  tipo de dominio: `Business/Models/Users/UserRequestModel.cs` (namespace
  `{root}.Business.Models.Users`); los compartidos van en `Models/Common/` (p. ej.
  `PaginationModel`). El `ResponseModel` incluye `int Id`, el `RequestModel` no.
- **Un mapper por entidad**: `{Entity}Mapper.cs` (Mapperly, una clase `[Mapper]` por entidad, con
  `[MapperIgnoreTarget]` para `Id`/`CreatedAt`/`UpdatedAt`/`Deleted`) o `{Entity}Profile.cs`
  (AutoMapper, un `Profile` por entidad). Nunca un mapper agrupado por módulo. Ejemplo:
  ```csharp
  [Mapper]
  public partial class EditionMapper
  {
      public partial Edition ToEditionEntity(EditionRequestModel source);
      public partial EditionResponseModel ToEditionResponseModel(Edition source);
  }
  ```
- Nombres de tabla por defecto: plural **en inglés** (`Category` → `Categories`,
  `Address` → `Addresses`, `Person` → `People`).

## Herramientas de verificación

- `verify_conventions(solutionPath)` — corre `verify-conventions.py` sobre una solución y devuelve
  `{passed, exitCode, output}`. Requiere `python3`.
- `generate_conventions_check` — emite/actualiza en `scripts/`: `check-conventions.py`,
  `check-english-identifiers.py`, `verify-conventions.py` y `english-allowlist.txt` (nunca
  se pisa). `generate_solution_layout` los siembra automáticamente.
- Línea de comandos: `python3 scripts/verify-conventions.py [--allow a,b] [ruta]` (exit 1 si hay
  hallazgos). Códigos: **V1** un tipo por archivo / archivo = tipo / namespace = carpeta; **V2**
  identificadores en español (diccionario de palabras comunes, ignora strings y comentarios; salida
  `archivo:línea:identificador`); **V3** modelos en carpeta por entidad; **V4** un mapper por
  entidad; **V5** servicios heredan `BaseService`; **V6** repositorios heredan `BaseRepository`;
  **V7** tipos de infraestructura que no existen en el MCP (carpetas `Base/`/`Common/`, sufijos
  `Base`, `Clock`, `Helper`...) se listan como **desvíos** y solo pasan si el tipo está registrado en
  `SLICES.md` bajo "Desvíos de la skill/MCP"; **V8** (advertencia no bloqueante) comentarios en
  inglés.
- Allowlist del detector de español: `scripts/english-allowlist.txt` (una palabra por línea,
  minúsculas, `#` comentarios) o `--allow palabra1,palabra2`. Usarla solo para nombres propios o
  palabras válidas también en inglés, y registrar cada entrada en `SLICES.md`.
- `check-conventions.py` y `check-english-identifiers.py` también se pueden correr por separado.

## Errores HTTP: `ErrorType` + `ApiControllerBase`

La plantilla trae `Domain/Common/ErrorType.cs` (`Validation`=400, `Unauthorized`=401,
`Forbidden`=403, `NotFound`=404, `Conflict`=409, `Locked`=423, `Unexpected`=500) y
`MResult<T>.Fail(message, errorType = Unexpected)` con la propiedad `ErrorType`. La tool
`generate_api_controller_base` (y `generate_solution_layout` cuando recibe `apiProjectPath`) genera
`Api/Controllers/ApiControllerBase.cs`, cuyo `ToActionResult(MResult<T>)` traduce `ErrorType` al
código HTTP y devuelve siempre el `MResult` como cuerpo. `BaseService` ya usa `ErrorType.NotFound`.

## Tests del scaffold

```bash
dotnet test tests/ArjuyScaffold.Tests
```

### Tools

| Tool | Qué genera | Estado |
|------|-----------|--------|
| `generate_entity` | Clase de entidad Domain que hereda `Entity` (o el nombre que le pases), con propiedades auto-implementadas | Completo |
| `generate_service` | `I{Entity}Service` / `{Entity}Service` en **archivos separados** (`Business/Interfaces/` y `Business/Services/`), la clase hereda `BaseService<TEntity, TResponseModel>` (verificado contra `ArjuyTurismo.Business`) e implementa solo `MapToResponse` + métodos custom — NO reimplementa `GetByIdAsync`/`GetAllAsync`/`DeleteAsync`, esos ya los da la base. Todo método (custom) devuelve `Task<MResult<T>>`, try/catch → `MResult<T>.Fail(...)` | Completo — ver sección dedicada abajo |
| `generate_repository` | Par `I{Name}Repository` / `{Name}Repository`, ambos extienden el genérico `IBaseRepository<T>`/`BaseRepository<T>` (CRUD heredado: `GetByIdAsync`, `GetAllAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ExistsAsync`), constructor solo reenvía el DbContext a `base(context)`, métodos custom opcionales como `Task`/`Task<T>` planos (sin `MResult<T>`, sin try/catch) | Completo |
| `generate_ef_configuration` | `Persistence.Database/Configurations/{Entity}Configuration.cs`, `IEntityTypeConfiguration<T>` con `ToTable`, `HasKey`, `HasQueryFilter(soft delete)` (omitido si `isLookup=true`), `Property(...)` por campo y relaciones `HasMany/HasOne` + `WithOne/WithMany` + `HasForeignKey` + `OnDelete` | Completo |
| `generate_dto` | UN archivo por modelo: `Business/Models/{PluralEntity}/{Entity}ResponseModel.cs` y `{Entity}RequestModel.cs`, propiedades planas, sin Data Annotations, strings con default `= string.Empty` | Completo |
| `generate_mapper` | Código de mapeo Entity ↔ DTO para **Mapperly** o **AutoMapper**, según el parámetro **`provider` (REQUERIDO, sin default)** — ver sección dedicada abajo | Completo |
| `generate_feature` | Tool compuesta: orquesta `generate_entity` + `generate_repository` + `generate_service` + `generate_ef_configuration` + `generate_dto` + `generate_mapper` para UNA entidad en una sola llamada, reutilizando las mismas funciones estáticas (nada duplicado) | Completo |
| `verify_conventions` | Verifica una solución (V1–V8, ver "Herramientas de verificación") y devuelve los hallazgos | Completo |
| `generate_conventions_check` | Emite `scripts/` (check-conventions, check-english-identifiers, verify-conventions, allowlist) | Completo |
| `generate_api_controller_base` | `Api/Controllers/ApiControllerBase.cs` (ErrorType -> HTTP) | Completo |
| `generate_middleware_config` | `Api/ConfigServices/{Name}Config.cs` como extension method de `IServiceCollection` (y opcionalmente de `WebApplication`) | Completo |
| `generate_di_registration` | `DependencyInjectionConfig.cs` con `RegisterAssembly(...)` por reflexión | **Stub/incompleto** — ver abajo |
| `generate_solution_layout` | Bibliotecas de clase REALES (`.csproj`) para Domain/Persistence.Database/Repository/Business, agregadas a un `.slnx`/`.sln` con `ProjectReference` correctos (verificado contra `arjuyTurismo/ArjuyTurismoApp`); opcionalmente wirea un proyecto Api existente | Completo — única tool que ejecuta `dotnet` CLI, ver abajo |

### `generate_solution_layout` — la única tool con efectos colaterales reales

Todas las demás tools solo devuelven strings (y opcionalmente escriben UN archivo/carpeta de
archivos si se pide). Esta tool en cambio **ejecuta la CLI de `dotnet`** (`dotnet new classlib`,
`dotnet new sln`, `dotnet sln add`, `dotnet add reference`) porque no hay forma de crear un
proyecto de verdad escribiendo un string a disco — un `.csproj` sin estar dado de alta en la
solución y sin sus `ProjectReference` no es una biblioteca separada, es un archivo suelto.

Grafo de dependencias verificado contra el `.csproj` real de cada capa en `ArjuyTurismoApp`:

```
{namespaceRoot}.Domain                    (sin dependencias)
{namespaceRoot}.Persistence.Database  →   Domain
{namespaceRoot}.Repository             →   Domain, Persistence.Database
{namespaceRoot}.Business               →   Domain, Persistence.Database, Repository
{apiProjectPath} (si se pasa)          →   las 4 capas DIRECTAMENTE (no solo Business —
                                            así está en ArjuyTurismo.Api.csproj real)
```

Es **idempotente**: si un proyecto ya existe lo saltea (no lo pisa) pero igual asegura que esté
agregado a la solución y con sus referencias — se puede correr de nuevo sin romper nada.

**`includeBaseInfrastructure` (default `true`)** — además siembra las clases base que
`generate_entity`/`generate_repository`/`generate_service` YA ASUMEN que existen (verificado
archivo por archivo contra `ArjuyTurismoApp`, no inventado):

```
{namespaceRoot}.Domain/Common/ArgentinaTime.cs
{namespaceRoot}.Domain/Common/Entity.cs
{namespaceRoot}.Domain/Common/MResult.cs
{namespaceRoot}.Persistence.Database/AppDbContext.cs   (vacío, sin DbSets — se agregan a mano por entidad,
                                                         pero YA trae modelBuilder.ApplyConfigurationsFromAssembly(...)
                                                         en OnModelCreating, así que las EF Configurations que
                                                         genera generate_ef_configuration no necesitan registro manual)
{namespaceRoot}.Repository/Base/IBaseRepository.cs
{namespaceRoot}.Repository/Base/BaseRepository.cs
{namespaceRoot}.Business/Base/IBaseService.cs      (*)
{namespaceRoot}.Business/Base/BaseService.cs
```

(*) `IBaseService<TEntity, TResponseModel>` **NO existe en el `ArjuyTurismoApp` real** — ese
proyecto solo tiene `BaseService.cs` sin interface. Se agregó por pedido explícito para que
`Business/Base` siga el mismo patrón interface+clase que `Repository/Base`, no porque se haya
verificado contra el ejemplo. Documentado acá para que quede claro que es una convención propia,
no una convención de `arjuy*`.

También agrega `Microsoft.EntityFrameworkCore` + `Microsoft.EntityFrameworkCore.SqlServer` como
`PackageReference` de `{namespaceRoot}.Persistence.Database` (los necesita `AppDbContext`/
`BaseRepository`). Esos paquetes fluyen transitivamente a `Repository` y `Business` vía
`ProjectReference` — no hace falta agregarlos de nuevo en cada proyecto (así es como `BaseService`
usa `ILogger` sin tener su propio `PackageReference` a logging: lo hereda transitivamente de la
cadena EF Core → Persistence.Database → Repository → Business).

Cada archivo se escribe solo si **no existe todavía** (`WriteIfMissing`) — nunca pisa algo que el
usuario ya haya editado a mano. Pasar `includeBaseInfrastructure: false` si se quiere el layout de
proyectos sin esta siembra (por ejemplo, para armar la base infra distinto a mano).

**Limitación conocida**: fuera de EF Core en Persistence.Database, no copia otros `PackageReference`
(AutoMapper, Mapperly, MailKit, etc.) — esos quedan manuales (`dotnet add package ...`) porque son
decisión del proyecto destino, no una convención fija.

### `generate_mapper` — por qué `provider` no tiene default

AutoMapper y Mapperly no son intercambiables por una razón de negocio, no solo técnica: **AutoMapper
requiere licencia paga para empresas grandes** (modelo de Eyrium desde 2024), mientras que
**Mapperly es MIT/gratuito** (source generator de Roslyn, sin costo de reflexión en runtime). Cuál
usar depende de la situación de licenciamiento del cliente destino — por eso `provider` es
`string` **obligatorio sin valor por defecto**: fuerza a quien llama (humano u orquestador) a
tomar esa decisión conscientemente en cada llamada, en vez de heredar un default silencioso que
podría ser la opción incorrecta (con costo de licencia) para ese cliente.

Ambas convenciones fueron **verificadas contra código real**, no inventadas:

- **Mapperly** — granularidad **por ENTIDAD** (un archivo `{Entity}Mapper.cs` por entidad; ya no
  se agrupa por módulo, el parámetro `moduleName` fue eliminado) — `[Mapper] public partial class {Entity}Mapper` con métodos
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

### `generate_service` — asume que `BaseService<TEntity, TResponseModel>` ya existe

Mismo criterio que `generate_entity` con la clase `Entity`: esta tool **no genera** `BaseService`
(la clase base abstracta con `GetByIdAsync`/`GetAllAsync`/`DeleteAsync` + `MapToResponse` abstracto,
verificada en `ArjuyTurismo.Business/Base/BaseService.cs`) — asume que ya existe en el proyecto
destino. Si no existe todavía, hay que crearla a mano una sola vez antes de usar `generate_service`
o `generate_feature`.

La interface generada **declara igual** las 3 firmas heredadas (`GetAllAsync`, `GetByIdAsync`,
`DeleteAsync`) aunque la clase no las reimplemente — en C# los métodos `public virtual` de la clase
base satisfacen la interface de la clase derivada sin redeclararlos, exactamente como
`ICategoryService`/`CategoryService` en el proyecto real.

La clase mantiene además su **propio campo de repositorio con tipo específico**
(`I{Entity}Repository`, no solo el `IBaseRepository<TEntity>` genérico de la base) para consultas
custom — se lo pasa a `base(...)` también, igual que `_categoryRepository` en el ejemplo real.

### `generate_feature` — qué NO automatiza

- No toca `AppDbContext` — agregar el `DbSet<{Entity}>` queda como paso manual (documentado en el
  resultado de la tool).
- No registra el mapper en DI — para AutoMapper es un paso único por ensamblado (ver arriba), para
  Mapperly no aplica (no requiere DI).
- No genera controller CRUD — queda para una próxima iteración (la base `ApiControllerBase` sí se
  genera con `generate_api_controller_base`; los controllers la heredan y usan `ToActionResult`).

### `generate_feature` — `outputDirectoryPath` escribe PROYECTOS, no carpetas de un solo proyecto

`outputDirectoryPath` apunta a la carpeta **raíz de la solución** (donde vive el `.sln`), no a un
único proyecto. Cada capa se escribe en su propia carpeta de proyecto hermana, prefijada con
`namespaceRoot` (ej. `ArjuyTurismo`):

```
{outputDirectoryPath}/
  {namespaceRoot}.Domain/Entities/{Entity}.cs
  {namespaceRoot}.Repository/Interfaces/I{Entity}Repository.cs
  {namespaceRoot}.Repository/Repositories/{Entity}Repository.cs
  {namespaceRoot}.Persistence.Database/Configurations/{Entity}Configuration.cs
  {namespaceRoot}.Business/Interfaces/I{Entity}Service.cs
  {namespaceRoot}.Business/Services/{Entity}Service.cs
  {namespaceRoot}.Business/Models/{PluralEntity}/{Entity}ResponseModel.cs
  {namespaceRoot}.Business/Models/{PluralEntity}/{Entity}RequestModel.cs
  {namespaceRoot}.Business/Mappers/{Entity}Mapper.cs        (provider=mapperly)
  {namespaceRoot}.Business/Mappings/{Entity}Profile.cs      (provider=automapper)
```

`Domain`, `Repository` y `Persistence.Database` son **proyectos separados** (cada uno necesita su
propio `.csproj`, que esta tool no genera). `Business` es **un solo proyecto** que agrupa
`Services/`, `Models/` y `Mappers`/`Mappings` como subcarpetas internas — no capas separadas. Si
`namespaceRoot` se omite, el prefijo cae a `Generated` (`Generated.Domain`, etc.).

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
- `generate_ef_configuration` pluraliza el nombre de tabla en inglés (`Pluralizer`: y→ies, s/x/z/ch/sh→es, irregulares como Person→People); para casos raros pasar `tableName`.
- `generate_dto` sigue el patrón `{Entity}ResponseModel`/`{Entity}RequestModel` (un solo modelo de
  request) documentado para esta tarea — nótese que el proyecto real `arjuyGym` en cambio separa
  `{Entity}CreateModel`/`{Entity}UpdateModel`; si el proyecto destino usa esa variante, ajustar el
  nombre de clase generado a mano (la forma/propiedades no cambian).
- Tests automatizados en `tests/ArjuyScaffold.Tests` (`dotnet test tests/ArjuyScaffold.Tests`); además hay smoke test manual vía stdio (ver abajo).
- `generate_entity` con `outputFilePath` (y `generate_feature` con `outputDirectoryPath`)
  sobrescriben el archivo sin pedir confirmación ni chequear si ya existe — comportamiento
  aceptable para un prototipo, pero a revisar antes de un uso real.
- `generate_feature` no genera controller CRUD ni toca `AppDbContext`/registro de DI del mapper —
  ver la sección dedicada arriba.
- `generate_feature` con `outputDirectoryPath` crea las carpetas de proyecto (`{Prefix}.Domain`,
  `{Prefix}.Repository`, `{Prefix}.Persistence.Database`, `{Prefix}.Business`) pero **no genera los
  `.csproj`** de cada una ni las agrega al `.sln` — eso sigue siendo paso manual
  (`dotnet new classlib` + `dotnet sln add` + referencias entre proyectos por capa).

## Cómo compilar

Windows:

```powershell
cd C:\Users\floaj\OneDrive\Documentos\Desarrollo\mcp\arjuy-scaffold
dotnet build -c Release
```

Linux:

```bash
cd ~/Documents/Desarrollo/arjuy-scaffold
dotnet build -c Release
```

Después de cada cambio hay que volver a compilar y reiniciar la sesión de Claude Code (el server
se registra apuntando al `.dll` de Release).

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

### Linux (registro a nivel usuario, disponible en todos los proyectos)

Con .NET instalado vía mise, usar la ruta absoluta de `dotnet` y pasarle `PATH`/`DOTNET_ROOT`,
porque `generate_solution_layout` ejecuta la CLI de `dotnet`:

```bash
D=~/.local/share/mise/dotnet-root
claude mcp add --scope user arjuy-scaffold \
  -e DOTNET_ROOT=$D -e "PATH=$D:/usr/local/bin:/usr/bin:/bin" \
  -- $D/dotnet ~/Documents/Desarrollo/arjuy-scaffold/bin/Release/net10.0/arjuy-scaffold.dll
claude mcp get arjuy-scaffold   # debe decir "Connected"
```

## Cambios de la iteración 3

- `generate_entity` agrega `using {Root}.Domain.Common;` (derivado de `namespaceName`, o vía el
  nuevo parámetro `baseEntityNamespace`) y inicializa `string` con `= string.Empty` y las colecciones
  con `= new List<T>()`: la entidad generada compila sin warnings con `Nullable=enable`.
- `ArgentinaTime` (sembrado por `generate_solution_layout`) busca primero la zona IANA
  `America/Argentina/Buenos_Aires`, después `Argentina Standard Time` (Windows) y, si el sistema no
  tiene base de zonas horarias (contenedores mínimos), cae a UTC-3 fijo.
- `generate_solution_layout` ejecuta `dotnet` con `ProcessStartInfo.ArgumentList` (cada argumento
  por separado: rutas con espacios o comillas ya no se re-interpretan) y valida que `namespaceRoot`
  sea un namespace C# válido.
- Verificado en Linux: layout completo en una ruta con espacios + entidad generada → `dotnet build`
  de la solución generada con 0 errores y 0 warnings.

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
