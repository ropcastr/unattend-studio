using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AutoUnattendStudio;

public sealed class UserConfig
{
    public string Language { get; set; } = "pt-BR";
    public bool Dark { get; set; } = true;
    public bool ThemePreferenceSet { get; set; }
    public Dictionary<string, bool> Flags { get; set; } = new();
    public Dictionary<string, string> Fields { get; set; } = new();
}

public partial class MainWindow : Window
{
    private sealed class ConfirmingCheckBox : CheckBox
    {
        public Func<bool>? ConfirmBeforeCheck { get; set; }
        protected override void OnToggle()
        {
            if (IsChecked != true && ConfirmBeforeCheck is not null && !ConfirmBeforeCheck()) return;
            base.OnToggle();
        }
    }
    private sealed record SelectItem(string Value, string Label);
    private static readonly FontFamily StudioFont = new(new Uri("pack://application:,,,/"), "./Fonts/#Recursive Sans Linear Static");
    private UserConfig state = new();
    private TextBox preview = null!;
    private TextBlock status = null!;
    private readonly Dictionary<string, string> labels = new();
    private static readonly Dictionary<string, string> ptBrCatalogLabels = LoadPtBrCatalogLabels();
    private readonly string[] languages = ["pt-BR", "en-US", "es-ES"];
    private readonly List<OptimizationOption> catalog = LoadCatalog();
    private string previewMode = "xml";
    private bool updatingOptions;
    private readonly (string Id, string Name, string Category)[] apps =
    [
        ("Google.Chrome", "Google Chrome", "Navegador"), ("Mozilla.Firefox", "Mozilla Firefox", "Navegador"),
        ("7zip.7zip", "7-Zip", "Utilitários"), ("VideoLAN.VLC", "VLC", "Multimídia"),
        ("Notepad++.Notepad++", "Notepad++", "Utilitários"), ("voidtools.Everything", "Everything", "Utilitários"),
        ("Adobe.Acrobat.Reader.64-bit", "Adobe Acrobat Reader", "Utilitários"), ("Discord.Discord", "Discord", "Comunicação"),
        ("Microsoft.VisualStudioCode", "Visual Studio Code", "Desenvolvimento"), ("Git.Git", "Git", "Desenvolvimento"),
        ("Valve.Steam", "Steam", "Jogos"), ("Spotify.Spotify", "Spotify", "Multimídia"),
        ("DominikReichl.KeePass", "KeePass", "Segurança"), ("ShareX.ShareX", "ShareX", "Utilitários")
    ];
    private readonly (string Package, string Name, string Category)[] removableApps =
    [
        ("Microsoft.Windows.Photos", "Fotos", "Microsoft"), ("Microsoft.XboxGamingOverlay", "Xbox Game Bar", "Xbox"),
        ("Microsoft.Xbox.TCUI", "Xbox TCUI", "Xbox"), ("Microsoft.BingNews", "Microsoft Notícias", "Microsoft"),
        ("Microsoft.GetHelp", "Obter Ajuda", "Microsoft"), ("Microsoft.Getstarted", "Dicas", "Microsoft"),
        ("Microsoft.WindowsFeedbackHub", "Hub de Comentários", "Microsoft"), ("Microsoft.People", "Pessoas", "Microsoft"),
        ("Microsoft.YourPhone", "Vincular ao Celular", "Microsoft"), ("Microsoft.MicrosoftSolitaireCollection", "Microsoft Solitaire", "Microsoft"),
        ("Clipchamp.Clipchamp", "Clipchamp", "Microsoft"), ("Microsoft.ZuneMusic", "Media Player", "Microsoft")
    ];

    private static List<OptimizationOption> LoadCatalog()
    {
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("AutoUnattendStudio.optimization-catalog.json");
        return stream is null ? [] : JsonSerializer.Deserialize<List<OptimizationOption>>(stream) ?? [];
    }

    public MainWindow() { InitializeComponent(); LoadPreferences(); BuildUi(); }

    private string L(string key) => labels.TryGetValue(key, out var value) ? value : key;
    private string LocalizeCatalogLabel(string source) => state.Language == "pt-BR" && ptBrCatalogLabels.TryGetValue(source, out var translated) ? translated : source;
    private static Dictionary<string, string> LoadPtBrCatalogLabels()
    {
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("AutoUnattendStudio.option-translations-pt-BR.json");
        return stream is null ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
    }
    private void T(string key, string pt, string en, string es) => labels[key] = state.Language switch { "en-US" => en, "es-ES" => es, _ => pt };
    private void BuildLabels()
    {
        labels.Clear();
        T("subtitle", "Gerador local de arquivo de resposta", "Local answer file generator", "Generador local de archivos de respuesta");
        T("system", "Sistema", "System", "Sistema"); T("privacy", "Privacidade", "Privacy", "Privacidad");
        T("performance", "Desempenho", "Performance", "Rendimiento"); T("interface", "Interface", "Interface", "Interfaz");
        T("apps", "Apps e IA", "Apps & AI", "Apps e IA"); T("services", "Serviços e tarefas", "Services & tasks", "Servicios y tareas");
        T("updates", "Windows Update", "Windows Update", "Windows Update"); T("post", "Pós-instalação", "Post-install", "Posinstalación");
        T("systemDesc", "Escolha a edição, o idioma e o comportamento da instalação.", "Choose the edition, language and installation behavior.", "Elige la edición, el idioma y el comportamiento de instalación.");
        T("privacyDesc", "Reduza sugestões e coleta opcional de dados após o primeiro login.", "Reduce suggestions and optional data collection after first sign-in.", "Reduce sugerencias y recopilación opcional de datos tras el primer inicio.");
        T("perfDesc", "Ajustes individuais de CPU, memória, GPU, latência, energia e animações. Cada item mostra o valor que será aplicado.", "Individual CPU, memory, GPU, latency, power and animation settings. Each item shows the value to apply.", "Ajustes individuales de CPU, memoria, GPU, latencia, energía y animaciones. Cada opción muestra el valor aplicado.");
        T("interfaceDesc", "Defina preferências visuais e do Explorador.", "Set visual and File Explorer preferences.", "Configura preferencias visuales y del Explorador.");
        T("appsDesc", "Remova apenas os pacotes selecionados após o primeiro login.", "Remove only selected packages after first sign-in.", "Elimina solo los paquetes seleccionados después del primer inicio.");
        T("servicesDesc", "Desative ou altere a inicialização de serviços e tarefas agendadas que você não usa.", "Disable or change startup for services and scheduled tasks you do not use.", "Desactiva o cambia el inicio de servicios y tareas que no utilizas.");
        T("updatesDesc", "Controle reinicializações, drivers, entrega de atualizações e políticas do Windows Update.", "Control restarts, drivers, delivery optimization and Windows Update policies.", "Controla reinicios, controladores, entrega de actualizaciones y políticas de Windows Update.");
        T("postDesc", "Crie um script local de instalação com winget na área de trabalho.", "Create a local winget installer script on the desktop.", "Crea un script local de instalación con winget en el escritorio.");
        T("postSecurity", "Os ajustes e remoções escolhidos serão executados como administrador no primeiro login. Revise a prévia do PowerShell.", "Selected tweaks and removals run as administrator at first sign-in. Review the PowerShell preview.", "Los ajustes y eliminaciones seleccionados se ejecutan como administrador en el primer inicio. Revisa la vista previa de PowerShell.");
        T("presetStart", "COMECE COM UM PRESET", "START WITH A PRESET", "EMPIEZA CON UN PRESET");
        T("previewLive", "AO VIVO", "LIVE", "EN VIVO"); T("previewHint", "Arquivo local · atualização instantânea", "Local file · updates as you edit", "Archivo local · se actualiza al editar");
        T("copy", "Copiar", "Copy", "Copiar"); T("copied", "Prévia copiada.", "Preview copied.", "Vista previa copiada.");
        T("os", "Versão do Windows", "Windows version", "Versión de Windows"); T("edition", "Edição", "Edition", "Edición");
        T("locale", "Idioma do Windows", "Windows language", "Idioma de Windows"); T("keyboard", "Teclado", "Keyboard", "Teclado");
        T("timezone", "Fuso horário", "Time zone", "Zona horaria"); T("computer", "Nome do computador", "Computer name", "Nombre del equipo");
        T("imageSelect", "Selecionar imagem do Windows por", "Select Windows image by", "Seleccionar imagen de Windows por");
        T("imageEdition", "Edição escolhida acima", "Edition selected above", "Edición seleccionada arriba"); T("imageName", "Nome", "Name", "Nombre"); T("imageIndex", "Índice", "Index", "Índice"); T("imageDescription", "Descrição", "Description", "Descripción");
        T("imageSelectorValue", "Valor da imagem (nome, índice ou descrição)", "Image value (name, index, or description)", "Valor de imagen (nombre, índice o descripción)");
        T("imageSelectorHint", "Use o índice ou nome exibido por DISM /Get-ImageInfo para a mídia que você vai instalar.", "Use the index or name shown by DISM /Get-ImageInfo for the media you will install.", "Usa el índice o nombre que muestra DISM /Get-ImageInfo para el medio que instalarás.");
        T("fullName", "Nome completo (opcional)", "Full name (optional)", "Nombre completo (opcional)"); T("organization", "Organização (opcional)", "Organization (optional)", "Organización (opcional)");
        T("activationKey", "Chave de ativação do Windows (opcional)", "Windows activation key (optional)", "Clave de activación de Windows (opcional)");
        T("activationKeyHint", "A chave será aplicada pelo script pós-instalação e deve corresponder à edição escolhida. Ela fica legível no XML/script gerado; não a compartilhe.", "The key is applied by the post-install script and must match the selected edition. It is readable in the generated XML/script; keep it private.", "La clave se aplica con el script posterior y debe coincidir con la edición elegida. Queda legible en el XML/script; no la compartas.");
        T("activateOnline", "Tentar ativar online após instalar a chave (requer internet)", "Try online activation after installing the key (requires internet)", "Intentar activar en línea después de instalar la clave (requiere internet)");
        T("accountType", "Tipo da conta local", "Local account type", "Tipo de cuenta local"); T("accountAdmin", "Administrador", "Administrator", "Administrador"); T("accountStandard", "Usuário padrão", "Standard user", "Usuario estándar");
        T("dynamicUpdate", "Baixar atualizações durante a instalação (Dynamic Update)", "Download updates during setup (Dynamic Update)", "Descargar actualizaciones durante la instalación (Dynamic Update)");
        T("dynamicUpdateHint", "Quando ativado, o Windows Setup pode buscar arquivos e drivers mais recentes pela internet. Desative para manter a instalação offline.", "When enabled, Windows Setup can fetch newer setup files and drivers over the internet. Disable to keep setup offline.", "Al activarlo, Windows Setup puede buscar archivos y controladores más recientes por internet. Desactívalo para mantener la instalación sin conexión.");
        T("targetDisk", "Número do disco de destino", "Target disk number", "Número del disco de destino");
        T("formatSection", "Formatação automática", "Automatic formatting", "Formato automático");
        T("formatSectionHint", "Define se o Windows Setup deve apagar e particionar o disco de destino automaticamente.", "Choose whether Windows Setup should erase and partition the target disk automatically.", "Define si Windows Setup debe borrar y particionar automáticamente el disco de destino.");
        T("diskUnlockHint", "Ative a opção acima para habilitar a escolha do disco.", "Enable the option above to choose the target disk.", "Activa la opción anterior para elegir el disco de destino.");
        T("ntfsInstallNote", "A partição do Windows será formatada em NTFS. ReFS não é oferecido para inicializar Windows 10/11 cliente; pode ser considerado para um volume de dados separado no futuro.", "The Windows partition will use NTFS. ReFS is not offered for booting Windows 10/11 client; it may be considered for a separate data volume later.", "La partición de Windows usará NTFS. No se ofrece ReFS para iniciar Windows 10/11 cliente; podría considerarse para un volumen de datos separado en el futuro.");
        T("createRestore", "Gerar script de restauração para ajustes reversíveis", "Create a restore script for reversible changes", "Crear un script de restauración para cambios reversibles");
        T("restoreHint", "O script salva o estado anterior de Registro, tarefas e energia antes das alterações. Execute a restauração na mesma conta. Apps/recursos removidos, Edge/OneDrive e alguns layouts do Iniciar não podem ser restaurados automaticamente.", "The script saves the previous registry, task, and power state. Run it under the same account. Removed apps/features, Edge/OneDrive, and some Start layouts cannot be restored automatically.", "El script guarda el estado previo del Registro, tareas y energía. Ejecútalo con la misma cuenta. Las apps/características quitadas, Edge/OneDrive y algunos diseños de Inicio no se restauran automáticamente.");
        T("validationTitle", "Revise a configuração", "Review configuration", "Revisa la configuración"); T("validationErrors", "Corrija estes erros antes de salvar:", "Fix these errors before saving:", "Corrige estos errores antes de guardar:"); T("validationWarnings", "Avisos:", "Warnings:", "Avisos:");
        T("imageValueRequired", "Informe o nome, índice ou descrição da imagem selecionada.", "Enter the selected image name, index, or description.", "Escribe el nombre, índice o descripción de la imagen seleccionada.");
        T("imageIndexInvalid", "O índice da imagem deve ser um número inteiro maior que zero.", "The image index must be a whole number greater than zero.", "El índice de imagen debe ser un entero mayor que cero.");
        T("activationKeyInvalid", "A chave de ativação deve ter cinco grupos de cinco letras ou números separados por hífen.", "The activation key must have five groups of five letters or digits separated by hyphens.", "La clave debe tener cinco grupos de cinco letras o números separados por guiones.");
        T("diskInvalid", "O número do disco deve ser um inteiro não negativo.", "The disk number must be a non-negative integer.", "El número de disco debe ser un entero no negativo.");
        T("computerInvalid", "O nome do computador deve ter de 1 a 15 caracteres: letras, números ou hífen, sem ser apenas numérico.", "The computer name must be 1–15 characters: letters, numbers, or hyphens, and not all numeric.", "El nombre del equipo debe tener de 1 a 15 caracteres: letras, números o guiones, y no ser solo numérico.");
        T("usernameInvalid", "O nome da conta deve ter até 20 caracteres e não pode conter \\/[]:;|=,+*?<>.", "The account name must be 20 characters or fewer and cannot contain \\/[]:;|=,+*?<>.", "El nombre de cuenta debe tener hasta 20 caracteres y no puede contener \\/[]:;|=,+*?<>.");
        T("xmlCharactersInvalid", "Um ou mais campos contêm caracteres que não podem ser gravados em XML.", "One or more fields contain characters that cannot be written to XML.", "Uno o más campos contienen caracteres que no se pueden escribir en XML.");
        T("postNeedsAdmin", "A conta padrão pode exigir credenciais administrativas no primeiro login; se não houver administrador, o script pós-instalação não poderá aplicar ajustes do sistema.", "A standard account may require administrator credentials at first sign-in; without an administrator, the post-install script cannot apply system changes.", "Una cuenta estándar puede pedir credenciales de administrador al iniciar sesión; sin administrador, el script no podrá aplicar cambios del sistema.");
        T("activationManual", "A chave ficará no script da Área de Trabalho e só será aplicada quando ele for executado.", "The key will be in the Desktop script and is applied only when that script runs.", "La clave quedará en el script del Escritorio y solo se aplicará al ejecutarlo.");
        T("noActivationKey", "Nenhuma chave foi informada; o script não tentará ativar o Windows.", "No key was entered; the script will not attempt activation.", "No se ingresó una clave; el script no intentará activar Windows.");
        T("activationXmlWarn", "A chave será incluída no payload compactado do XML para o script pós-instalação. O conteúdo é codificado, não criptografado.", "The key will be included in the compressed XML payload for the post-install script. It is encoded, not encrypted.", "La clave se incluirá en el contenido comprimido del XML para el script posterior. Está codificada, no cifrada.");
        T("localAccount", "Criar conta local durante a instalação", "Create a local account during setup", "Crear una cuenta local durante la instalación");
        T("username", "Nome de usuário", "User name", "Nombre de usuario"); T("password", "Senha da conta", "Account password", "Contraseña de la cuenta");
        T("accountHint", "A senha será gravada no XML em texto legível. Use um arquivo protegido e apague-o após instalar.", "The password is stored as readable text in the XML. Keep the file secure and delete it after setup.", "La contraseña se guarda como texto legible en el XML. Protege el archivo y elimínalo tras instalar.");
        T("wipe", "Apagar o disco e particionar para UEFI", "Erase disk and partition for UEFI", "Borrar el disco y particionar para UEFI");
        T("wipeHint", "OPERAÇÃO DESTRUTIVA: todos os dados do disco de destino selecionado serão apagados.", "DESTRUCTIVE: all data on the selected target disk will be erased.", "DESTRUCTIVO: se borrarán todos los datos del disco de destino seleccionado.");
        T("unattended", "Ocultar telas comuns do OOBE", "Skip common OOBE screens", "Omitir pantallas comunes de OOBE");
        T("privacy1", "Desativar experiências personalizadas e sugestões", "Disable tailored experiences and suggestions", "Desactivar experiencias personalizadas y sugerencias");
        T("privacy2", "Desativar ID de publicidade do usuário", "Disable the user advertising ID", "Desactivar el identificador de publicidad del usuario");
        T("privacy3", "Desativar histórico de atividades", "Disable activity history", "Desactivar el historial de actividad");
        T("performance1", "Desativar animações e transparência", "Disable animations and transparency", "Desactivar animaciones y transparencia");
        T("performance2", "Usar plano de energia de alto desempenho", "Use the high performance power plan", "Usar el plan de energía de alto rendimiento");
        T("interface1", "Mostrar extensões de arquivo", "Show file extensions", "Mostrar extensiones de archivo");
        T("interface2", "Mostrar arquivos ocultos", "Show hidden files", "Mostrar archivos ocultos");
        T("interface3", "Ativar modo escuro do Windows", "Enable Windows dark mode", "Activar el modo oscuro de Windows");
        T("interface4", "Usar menu de contexto clássico", "Use the classic context menu", "Usar el menú contextual clásico");
        T("remove", "Pacotes para remover", "Packages to remove", "Paquetes para eliminar");
        T("removeHint", "A remoção afeta a conta e o provisionamento local. Alguns apps podem ser reinstalados pelo Windows Update.", "Removal affects the account and local provisioning. Windows Update may reinstall some apps.", "La eliminación afecta a la cuenta y al aprovisionamiento local. Windows Update puede reinstalar algunas aplicaciones.");
        T("autoRun", "Executar script automaticamente no primeiro login", "Run the script automatically at first sign-in", "Ejecutar el script automáticamente en el primer inicio");
        T("selfDelete", "Apagar script após execução", "Delete the script after it runs", "Eliminar el script después de ejecutarlo");
        T("customIds", "IDs adicionais do winget (um por linha)", "Additional winget IDs (one per line)", "IDs adicionales de winget (uno por línea)");
        T("wingetHint", "A instalação dos programas requer internet e o winget disponível no Windows.", "Installing apps requires internet access and winget on Windows.", "Instalar aplicaciones requiere internet y winget en Windows.");
        T("preview", "PRÉVIA DO ARQUIVO", "FILE PREVIEW", "VISTA PREVIA DEL ARCHIVO"); T("ready", "Pronto para gerar", "Ready to generate", "Listo para generar");
        T("save", "Salvar XML", "Save XML", "Guardar XML"); T("load", "Abrir perfil", "Load profile", "Abrir perfil");
        T("saveProfile", "Salvar perfil", "Save profile", "Guardar perfil"); T("preset", "Preset", "Preset", "Preset");
        T("language", "Idioma", "Language", "Idioma"); T("theme", "Tema", "Theme", "Tema"); T("light", "Claro", "Light", "Claro"); T("dark", "Escuro", "Dark", "Oscuro");
        T("confirmTitle", "Confirmar apagamento", "Confirm disk erase", "Confirmar borrado");
        T("confirmWipe", "O XML vai apagar todas as partições do DISCO {disk}. Esta ação não pode ser desfeita. Confirme que selecionará o disco correto.", "The XML will erase all partitions on DISK {disk}. This cannot be undone. Confirm you will select the correct disk.", "El XML borrará todas las particiones del DISCO {disk}. Esta acción no se puede deshacer. Confirma que seleccionarás el disco correcto.");
        T("continue", "Deseja ativar o apagamento do disco 0?", "Enable disk 0 erasure?", "¿Activar el borrado del disco 0?");
        T("userRequired", "Informe o nome de usuário e a senha da conta local.", "Enter a user name and password for the local account.", "Introduce un nombre y una contraseña para la cuenta local.");
        T("created", "Arquivo salvo com sucesso.", "File saved successfully.", "Archivo guardado correctamente.");
        T("profileLoaded", "Perfil carregado.", "Profile loaded.", "Perfil cargado.");
        T("dangerFound", "O XML contém configurações sensíveis. Revise a prévia antes de usar.", "The XML contains sensitive settings. Review the preview before use.", "El XML contiene ajustes delicados. Revisa la vista previa antes de usarlo.");
        T("starter", "Essencial", "Essentials", "Esencial"); T("dev", "Desenvolvedor", "Developer", "Desarrollador"); T("gaming", "Jogos", "Gaming", "Juegos");
        T("profileWarn", "O perfil conterá a senha da conta e/ou a chave de ativação em texto legível. Armazene-o com segurança.", "The profile will contain the account password and/or activation key as readable text. Store it securely.", "El perfil contendrá la contraseña de la cuenta y/o la clave de activación en texto legible. Guárdalo de forma segura.");
        T("popularPrograms", "PROGRAMAS POPULARES", "POPULAR APPS", "APLICACIONES POPULARES");
        T("searchOptions", "Filtrar opções por nome ou descrição…", "Filter settings by name or description…", "Filtrar opciones por nombre o descripción…");
        T("selectedCount", "opções selecionadas", "settings selected", "opciones seleccionadas");
        T("clearSection", "Limpar esta seção", "Clear this section", "Limpiar esta sección");
        T("applySetting", "Aplicar este valor", "Apply this value", "Aplicar este valor");
        T("registryTarget", "Alvo", "Target", "Destino");
        T("caution", "ATENÇÃO", "CAUTION", "PRECAUCIÓN");
        T("critical", "RISCO ALTO", "HIGH RISK", "RIESGO ALTO");
        T("availableCount", "disponíveis", "available", "disponibles");
        T("optionsPrivacy", "Privacidade", "Privacy", "Privacidad");
        T("optionsNotifications", "Notificações", "Notifications", "Notificaciones");
        T("optionsSound", "Áudio e voz", "Audio & voice", "Audio y voz");
        T("optionsPerf", "Desempenho e jogos", "Performance & gaming", "Rendimiento y juegos");
        T("optionsPower", "Plano e energia", "Power plan & energy", "Plan y energía");
        T("optionsPowerSettings", "CPU, USB e energia", "CPU, USB & power", "CPU, USB y energía");
        T("optionsPowerReg", "Políticas de energia", "Power policies", "Políticas de energía");
        T("optionsExplorer", "Explorador de Arquivos", "File Explorer", "Explorador de archivos");
        T("optionsStart", "Menu Iniciar", "Start menu", "Menú Inicio");
        T("optionsTaskbar", "Barra de tarefas", "Taskbar", "Barra de tareas");
        T("optionsTheme", "Tema", "Theme", "Tema");
        T("optionsPackages", "Apps removíveis e IA", "Removable apps & AI", "Apps e IA que se pueden quitar");
        T("optionsCapabilities", "Recursos opcionais do Windows", "Optional Windows capabilities", "Características opcionales de Windows");
        T("optionsAdvanced", "Ações avançadas", "Advanced actions", "Acciones avanzadas");
        T("optionsServices", "Serviços", "Services", "Servicios");
        T("optionsTasks", "Tarefas agendadas", "Scheduled tasks", "Tareas programadas");
        T("optionsUpdates", "Políticas e comportamento", "Policies & behavior", "Políticas y comportamiento");
        T("targetScopeSystem", "Sistema", "System", "Sistema");
        T("targetScopeUser", "Conta de usuário", "User account", "Cuenta de usuario");
        T("confirmOptimization", "Esta alteração pode afetar recursos do Windows. Confira a descrição e o valor antes de continuar.", "This change may affect Windows features. Review its description and target value before continuing.", "Este cambio puede afectar funciones de Windows. Revisa la descripción y el valor antes de continuar.");
        T("removePrefix", "Remover", "Remove", "Quitar");
        T("removeRegistryValue", "Remover valor do Registro", "Remove registry value", "Quitar valor del Registro");
        T("removeRegistryKey", "Remover chave do Registro", "Remove registry key", "Quitar clave del Registro");
        T("createRegistryKey", "Criar chave do Registro", "Create registry key", "Crear clave del Registro");
        T("binaryFlag", "Sinalizador binário", "Binary flag", "Indicador binario"); T("binaryValue", "Valor binário", "Binary value", "Valor binario");
        T("removeAppxPackage", "Remover pacote Appx", "Remove Appx package", "Quitar paquete Appx"); T("removeCapability", "Remover recurso do Windows", "Remove Windows capability", "Quitar característica de Windows");
        T("disableTask", "Desativar tarefa agendada", "Disable scheduled task", "Desactivar tarea programada");
        T("acPower", "Na tomada", "AC", "Con corriente"); T("batteryPower", "Na bateria", "Battery", "Con batería");
        T("targetPowerPlan", "Criar e ativar plano baseado em Desempenho Máximo", "Create and activate an Ultimate Performance based plan", "Crear y activar un plan basado en Máximo rendimiento");
        T("targetEdgeRemoval", "Desinstalar o Microsoft Edge do sistema", "Uninstall Microsoft Edge system installation", "Desinstalar Microsoft Edge del sistema");
        T("targetOneDriveRemoval", "Desinstalar o OneDrive sem excluir arquivos sincronizados", "Uninstall the OneDrive client without deleting synced files", "Desinstalar OneDrive sin eliminar archivos sincronizados");
        T("targetCleanStart", "Limpar a lista padrão de itens fixados no menu Iniciar", "Clear the default pinned Start menu list", "Limpiar la lista predeterminada del menú Inicio");
        T("targetRebootAfter", "Reiniciar o Windows quando o script terminar", "Restart Windows after the script completes", "Reiniciar Windows al finalizar el script");
        T("edgeRemovalRisk", "O Edge e o WebView2 são usados por vários programas. O WebView2 será preservado.", "Edge and WebView2 are used by many applications. WebView2 will be kept.", "Muchas aplicaciones usan Edge y WebView2. WebView2 se conservará.");
        T("privacyPreset", "Privacidade", "Privacy", "Privacidad"); T("performancePreset", "Desempenho", "Performance", "Rendimiento");
    }

    private void BuildUi()
    {
        BuildLabels(); Title = "Unattend Studio"; Background = Brush("side"); Foreground = Brush("text"); FontFamily = StudioFont;
        var root = new Grid { Background = Brush("side") };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(216) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
        root.RowDefinitions.Add(new RowDefinition()); Content = root;

        var sidebar = new Border { Background = Brush("side"), Padding = new Thickness(0), ClipToBounds = false };
        Panel.SetZIndex(sidebar, 1);
        Grid.SetColumn(sidebar, 0); root.Children.Add(sidebar);
        var sideStack = new DockPanel(); sidebar.Child = sideStack;
        var brand = new StackPanel { Margin = new Thickness(20, 22, 18, 30) };
        brand.Children.Add(new TextBlock { Text = "U/", FontSize = 29, FontWeight = FontWeights.Bold, Foreground = Brush("sideAccent"), Margin = new Thickness(0, 0, 0, 7) });
        brand.Children.Add(new TextBlock { Text = "UNATTEND STUDIO", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brush("sideText") });
        brand.Children.Add(new TextBlock { Text = L("subtitle"), FontSize = 11, Foreground = Brush("sideMuted"), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });
        DockPanel.SetDock(brand, Dock.Top); sideStack.Children.Add(brand);

        var nav = new StackPanel(); var centerContent = new ContentControl(); var navButtons = new List<Button>();
        var pages = new[] { ("system", "01"), ("privacy", "02"), ("performance", "03"), ("interface", "04"), ("apps", "05"), ("services", "06"), ("updates", "07"), ("post", "08") };
        foreach (var (key, step) in pages)
        {
            var page = BuildPage(key);
            var label = new TextBlock { Text = L(key), FontSize = 13, FontWeight = FontWeights.Medium, Foreground = Brush(key == "system" ? "sideText" : "sideMuted"), VerticalAlignment = VerticalAlignment.Center };
            var number = new TextBlock { Text = step, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush("sideMuted"), Width = 28, VerticalAlignment = VerticalAlignment.Center };
            var content = new Grid { Margin = new Thickness(20, 0, 16, 0) }; content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); content.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(number, 0); content.Children.Add(number); Grid.SetColumn(label, 1); content.Children.Add(label);
            var button = new Button { Content = content, Tag = key, HorizontalContentAlignment = HorizontalAlignment.Stretch, Height = 46, Margin = new Thickness(0, 0, -12, 0), Background = Brush(key == "system" ? "surface" : "side"), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, Template = FlatButtonTemplate() };
            button.Click += (_, _) =>
            {
                centerContent.Content = page;
                foreach (var item in navButtons)
                {
                    var active = (string)item.Tag == key;
                    item.Background = Brush(active ? "surface" : "side");
                    if (item.Content is Grid layout)
                    {
                        if (layout.Children[0] is TextBlock n) n.Foreground = Brush(active ? "sideAccent" : "sideMuted");
                        if (layout.Children[1] is TextBlock t) { t.Foreground = Brush(active ? "sideText" : "sideMuted"); t.FontWeight = active ? FontWeights.SemiBold : FontWeights.Medium; }
                    }
                }
            };
            navButtons.Add(button); nav.Children.Add(button);
        }
        DockPanel.SetDock(nav, Dock.Top); sideStack.Children.Add(nav);

        var sideFoot = new StackPanel { Margin = new Thickness(20, 0, 18, 16) };
        sideFoot.Children.Add(new TextBlock { Text = L("presetStart"), FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = Brush("sideMuted"), Margin = new Thickness(0, 0, 0, 6) });
        var presets = new ComboBox { Height = 34, FontSize = 11, Background = Brush("sideControl"), Foreground = Brush("sideText"), BorderBrush = Brush("sideLine") };
        StyleCombo(presets, "sideControl", "sideText", "sideLine");
        presets.Items.Add(L("preset")); presets.Items.Add(L("starter")); presets.Items.Add(L("dev")); presets.Items.Add(L("gaming")); presets.Items.Add(L("privacyPreset")); presets.Items.Add(L("performancePreset")); presets.SelectedIndex = 0;
        presets.SelectionChanged += (_, _) => { if (presets.SelectedIndex > 0) { ApplyPreset(presets.SelectedIndex); BuildUi(); } }; sideFoot.Children.Add(presets);
        sideFoot.Children.Add(new TextBlock { Text = "●  LOCAL · WINDOWS 10 / 11", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = Brush("sideAccent"), Margin = new Thickness(0, 14, 0, 0) });
        DockPanel.SetDock(sideFoot, Dock.Bottom); sideStack.Children.Add(sideFoot);

        var sheetFrame = new Border { Background = Brush("surface"), Margin = new Thickness(12, 10, 12, 10), CornerRadius = new CornerRadius(14), ClipToBounds = true };
        var sheet = new Grid { Background = Brush("surface") }; sheetFrame.Child = sheet;
        Grid.SetColumn(sheetFrame, 1); root.Children.Add(sheetFrame);
        var main = new Grid { Margin = new Thickness(14, 22, 20, 18) }; main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); sheet.Children.Add(main);
        var toolbar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 22) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var language = new ComboBox { Width = 90, Height = 32, FontSize = 11, Background = Brush("surface"), Foreground = Brush("text"), BorderBrush = Brush("line"), Margin = new Thickness(0, 0, 7, 0) };
        StyleCombo(language, "surface", "text", "line");
        foreach (var lang in languages) language.Items.Add(lang); language.SelectedItem = state.Language;
        language.SelectionChanged += (_, _) => { if (language.SelectedItem is string value && state.Language != value) { state.Language = value; SavePreferences(); BuildUi(); } };
        var theme = Button(state.Dark ? L("light") : L("dark"), false); theme.MinWidth = 78; theme.Height = 32; theme.Margin = new Thickness(0, 0, 7, 0); theme.ToolTip = L("theme"); theme.Click += (_, _) => { state.Dark = !state.Dark; state.ThemePreferenceSet = true; SavePreferences(); BuildUi(); };
        var xmlSave = Button(L("save"), true); xmlSave.Height = 34; xmlSave.Click += (_, _) => SaveXml();
        actions.Children.Add(language); actions.Children.Add(theme); actions.Children.Add(xmlSave); DockPanel.SetDock(actions, Dock.Right); toolbar.Children.Add(actions);
        var pSave = Button(L("saveProfile"), false); pSave.Margin = new Thickness(0, 0, 7, 0); pSave.Click += (_, _) => SaveProfile();
        var pLoad = Button(L("load"), false); pLoad.Click += (_, _) => LoadProfile();
        var leftActions = new StackPanel { Orientation = Orientation.Horizontal }; leftActions.Children.Add(pSave); leftActions.Children.Add(pLoad); toolbar.Children.Add(leftActions);
        Grid.SetRow(toolbar, 0); main.Children.Add(toolbar);
        centerContent.Content = BuildPage("system"); Grid.SetRow(centerContent, 1); main.Children.Add(centerContent);
        status = new TextBlock { Text = L("ready"), Foreground = Brush("muted"), FontSize = 11, Margin = new Thickness(1, 12, 0, 0) }; Grid.SetRow(status, 2); main.Children.Add(status);

        var previewPanel = new Border { Background = Brush("previewBg"), BorderBrush = Brush("line"), BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(19, 23, 18, 18) };
        Grid.SetColumn(previewPanel, 2); root.Children.Add(previewPanel);
        var pv = new DockPanel(); previewPanel.Child = pv;
        var pvHead = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var previewTitle = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 5) };
        var previewName = new TextBlock { Text = L("preview"), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Brush("previewText"), VerticalAlignment = VerticalAlignment.Center };
        var live = new TextBlock { Text = "●  " + L("previewLive"), FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = Brush("previewAccent"), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(live, Dock.Right); previewTitle.Children.Add(live); previewTitle.Children.Add(previewName); pvHead.Children.Add(previewTitle);
        pvHead.Children.Add(new TextBlock { Text = L("previewHint"), FontSize = 10, Foreground = Brush("previewMuted"), Margin = new Thickness(0, 0, 0, 12) });
        var pvTools = new DockPanel { LastChildFill = false };
        var modeTabs = new StackPanel { Orientation = Orientation.Horizontal };
        var xmlTab = new Button { Content = "XML", Width = 66, Height = 30, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush("previewAccent"), Background = Brushes.Transparent, BorderBrush = Brush("previewAccent"), BorderThickness = new Thickness(0, 0, 0, previewMode == "xml" ? 2 : 0), Cursor = System.Windows.Input.Cursors.Hand, Template = GlassButtonTemplate(false) };
        var scriptTab = new Button { Content = "PowerShell", Width = 88, Height = 30, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush(previewMode == "script" ? "previewAccent" : "previewMuted"), Background = Brushes.Transparent, BorderBrush = Brush("previewAccent"), BorderThickness = new Thickness(0, 0, 0, previewMode == "script" ? 2 : 0), Cursor = System.Windows.Input.Cursors.Hand, Template = GlassButtonTemplate(false) };
        xmlTab.Foreground = Brush(previewMode == "xml" ? "previewAccent" : "previewMuted");
        void SetPreviewMode(string value)
        {
            previewMode = value;
            xmlTab.Foreground = Brush(value == "xml" ? "previewAccent" : "previewMuted");
            scriptTab.Foreground = Brush(value == "script" ? "previewAccent" : "previewMuted");
            xmlTab.BorderThickness = new Thickness(0, 0, 0, value == "xml" ? 2 : 0);
            scriptTab.BorderThickness = new Thickness(0, 0, 0, value == "script" ? 2 : 0);
            UpdatePreview();
        }
        xmlTab.Click += (_, _) => SetPreviewMode("xml"); scriptTab.Click += (_, _) => SetPreviewMode("script");
        modeTabs.Children.Add(xmlTab); modeTabs.Children.Add(scriptTab); DockPanel.SetDock(modeTabs, Dock.Left); pvTools.Children.Add(modeTabs);
        var copy = new Button { Content = L("copy"), Height = 30, Padding = new Thickness(10, 0, 10, 0), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush("previewText"), Background = Brush("previewControl"), BorderBrush = Brush("previewLine"), BorderThickness = new Thickness(1), Margin = new Thickness(7, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand, Template = GlassButtonTemplate(false) };
        copy.Click += (_, _) => { Clipboard.SetText(preview.Text ?? ""); status.Text = L("copied"); }; pvTools.Children.Add(copy); pvHead.Children.Add(pvTools);
        DockPanel.SetDock(pvHead, Dock.Top); pv.Children.Add(pvHead);
        preview = new TextBox { IsReadOnly = true, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = StudioFont, FontSize = 10.5, Background = Brushes.Transparent, Foreground = Brush("code"), BorderThickness = new Thickness(0), Padding = new Thickness(0, 5, 0, 0), IsUndoEnabled = false };
        pv.Children.Add(preview); UpdatePreview();
    }

    private UIElement BuildPage(string key)
    {
        var scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 4, 12, 18) };
        var stack = new StackPanel(); scroller.Content = stack;
        stack.Children.Add(new TextBlock { Text = L(key), FontSize = 31, FontWeight = FontWeights.SemiBold, Foreground = Brush("text"), Margin = new Thickness(0, 0, 0, 7) });
        var descKey = key switch { "system" => "systemDesc", "privacy" => "privacyDesc", "performance" => "perfDesc", "interface" => "interfaceDesc", "apps" => "appsDesc", "services" => "servicesDesc", "updates" => "updatesDesc", _ => "postDesc" };
        stack.Children.Add(new TextBlock { Text = L(descKey), FontSize = 12, Foreground = Brush("muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
        if (key == "system") BuildSystem(stack);
        if (key == "privacy") BuildCatalogOptions(stack, ["privacy", "notif", "sound"]);
        if (key == "performance") BuildCatalogOptions(stack, ["perf", "power-plan", "power-settings", "powerreg"]);
        if (key == "interface") BuildCatalogOptions(stack, ["explorer", "start", "taskbar", "theme"]);
        if (key == "apps") BuildApps(stack);
        if (key == "services") BuildCatalogOptions(stack, ["services", "tasks"]);
        if (key == "updates") BuildCatalogOptions(stack, ["wupdate"]);
        if (key == "post") BuildPost(stack);
        return scroller;
    }

    private void BuildSystem(Panel panel)
    {
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition());
        var os = new ComboBox { Height = 35, FontSize = 12 }; StyleCombo(os, "surface", "text", "line"); os.Items.Add("Windows 11"); os.Items.Add("Windows 10"); os.SelectedIndex = GetField("os", "Windows 11") == "Windows 10" ? 1 : 0; os.SelectionChanged += (_, _) => { SetField("os", os.SelectedItem?.ToString() ?? "Windows 11"); UpdatePreview(); };
        var ed = new ComboBox { Height = 35, FontSize = 12 }; StyleCombo(ed, "surface", "text", "line"); foreach (var x in new[] { "Pro", "Home", "Education", "Enterprise" }) ed.Items.Add(x); ed.SelectedItem = GetField("edition", "Pro"); if (ed.SelectedIndex < 0) ed.SelectedIndex = 0; ed.SelectionChanged += (_, _) => { SetField("edition", ed.SelectedItem?.ToString() ?? "Pro"); UpdatePreview(); };
        AddLabeled(row, 0, L("os"), os); AddLabeled(row, 1, L("edition"), ed); panel.Children.Add(row);
        AddLabel(panel, L("imageSelect"));
        var imageMode = new ComboBox { Height = 34, FontSize = 12, Margin = new Thickness(0, 0, 0, 7), ItemsSource = new[] { new SelectItem("edition", L("imageEdition")), new SelectItem("name", L("imageName")), new SelectItem("index", L("imageIndex")), new SelectItem("description", L("imageDescription")) }, DisplayMemberPath = "Label", SelectedValuePath = "Value" };
        StyleCombo(imageMode, "surface", "text", "line"); imageMode.SelectedValue = GetField("imageSelectionMode", "edition"); imageMode.SelectionChanged += (_, _) => { if (imageMode.SelectedValue is string value) SetField("imageSelectionMode", value); UpdatePreview(); }; panel.Children.Add(imageMode);
        Field(panel, "imageSelectorValue", L("imageSelectorValue"), "", false);
        panel.Children.Add(new TextBlock { Text = L("imageSelectorHint"), FontSize = 10, Foreground = Brush("muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(1, 0, 0, 10) });
        row = new Grid { Margin = new Thickness(0, 9, 0, 0) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition());
        var lang = new ComboBox { Height = 35, FontSize = 12 }; StyleCombo(lang, "surface", "text", "line"); foreach (var x in new[] { "pt-BR", "en-US", "es-ES" }) lang.Items.Add(x); lang.SelectedItem = GetField("locale", "pt-BR"); lang.SelectionChanged += (_, _) => { SetField("locale", lang.SelectedItem?.ToString() ?? "pt-BR"); UpdatePreview(); };
        var kbd = new ComboBox { Height = 35, FontSize = 12 }; StyleCombo(kbd, "surface", "text", "line"); foreach (var x in new[] { "Português ABNT2", "US", "Español" }) kbd.Items.Add(x); kbd.SelectedItem = GetField("keyboard", "Português ABNT2"); kbd.SelectionChanged += (_, _) => { SetField("keyboard", kbd.SelectedItem?.ToString() ?? "Português ABNT2"); UpdatePreview(); };
        AddLabeled(row, 0, L("locale"), lang); AddLabeled(row, 1, L("keyboard"), kbd); panel.Children.Add(row);
        var tz = Field(panel, "timezone", L("timezone"), "E. South America Standard Time", false);
        var comp = Field(panel, "computer", L("computer"), "", false);
        Field(panel, "fullName", L("fullName"), "", false);
        Field(panel, "organization", L("organization"), "", false);
        var account = Check("account", L("localAccount")); panel.Children.Add(account);
        AddLabel(panel, L("accountType"));
        var accountType = new ComboBox { Height = 34, FontSize = 12, Margin = new Thickness(0, 0, 0, 8), ItemsSource = new[] { new SelectItem("Administrators", L("accountAdmin")), new SelectItem("Users", L("accountStandard")) }, DisplayMemberPath = "Label", SelectedValuePath = "Value" };
        StyleCombo(accountType, "surface", "text", "line"); accountType.SelectedValue = GetField("accountType", "Administrators"); accountType.SelectionChanged += (_, _) => { if (accountType.SelectedValue is string value) SetField("accountType", value); UpdatePreview(); }; panel.Children.Add(accountType);
        Field(panel, "username", L("username"), "", false); Field(panel, "password", L("password"), "", true);
        panel.Children.Add(new TextBlock { Text = L("accountHint"), FontSize = 10.5, Foreground = Brush("warning"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(1, 5, 0, 14) });
        Field(panel, "activationKey", L("activationKey"), "", true);
        panel.Children.Add(new TextBlock { Text = L("activationKeyHint"), FontSize = 10, Foreground = Brush("warning"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(1, 3, 0, 8) });
        panel.Children.Add(Check("activateOnline", L("activateOnline")));
        panel.Children.Add(Check("dynamicUpdate", L("dynamicUpdate")));
        panel.Children.Add(new TextBlock { Text = L("dynamicUpdateHint"), FontSize = 10, Foreground = Brush("muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28, 2, 0, 8) });
        panel.Children.Add(Check("unattended", L("unattended")));
        panel.Children.Add(new TextBlock { Text = L("formatSection"), FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Brush("text"), Margin = new Thickness(0, 20, 0, 3) });
        panel.Children.Add(new TextBlock { Text = L("formatSectionHint"), FontSize = 10.5, Foreground = Brush("muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 9) });
        var wipe = Check("wipeDisk", L("wipe"), true, L("wipeHint"));
        panel.Children.Add(wipe);
        var wipeHint = new TextBlock { Text = L("wipeHint"), FontSize = 10.5, Foreground = Brush("danger"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28, 2, 0, 7) };
        panel.Children.Add(wipeHint);
        var targetDiskField = Field(panel, "targetDisk", L("targetDisk"), "0", false);
        var targetDiskLabel = panel.Children[panel.Children.Count - 2] as TextBlock;
        var diskUnlockHint = new TextBlock { Text = L("diskUnlockHint"), FontSize = 10, Foreground = Brush("muted"), Margin = new Thickness(1, -5, 0, 8), Visibility = GetFlag("wipeDisk") ? Visibility.Collapsed : Visibility.Visible };
        panel.Children.Add(diskUnlockHint);
        void UpdateDiskInputState(bool enabled)
        {
            targetDiskField.IsEnabled = enabled;
            if (targetDiskLabel is not null) { targetDiskLabel.IsEnabled = enabled; targetDiskLabel.Opacity = enabled ? 1 : .48; }
            diskUnlockHint.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            wipeHint.Foreground = Brush(enabled ? "danger" : "muted");
        }
        UpdateDiskInputState(GetFlag("wipeDisk"));
        wipe.Checked += (_, _) => UpdateDiskInputState(true);
        wipe.Unchecked += (_, _) => UpdateDiskInputState(false);
        wipe.ToolTip = L("wipeHint");
        panel.Children.Add(new TextBlock { Text = L("ntfsInstallNote"), FontSize = 10, Foreground = Brush("muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28, 0, 0, 10) });
    }

    private void BuildApps(Panel panel)
    {
        panel.Children.Add(new TextBlock { Text = L("removeHint"), FontSize = 11, Foreground = Brush("warning"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        BuildCatalogOptions(panel, ["apps-packages", "apps-caps", "apps-scripts", "usertask"]);
    }

    private void BuildCatalogOptions(Panel panel, string[] groups)
    {
        var options = catalog.Where(option => groups.Contains(option.Group)).OrderBy(option => option.Order).ToList();
        if (options.Count == 0) return;
        var toolsRow = new DockPanel { Margin = new Thickness(0, 0, 0, 9), LastChildFill = false };
        var filter = new TextBox { Width = 280, Height = 32, FontSize = 11, Text = "", ToolTip = L("searchOptions"), Background = Brush("surface"), Foreground = Brush("text"), BorderBrush = Brush("line"), Padding = new Thickness(8, 5, 8, 4) };
        DockPanel.SetDock(filter, Dock.Left); toolsRow.Children.Add(filter);
        var clear = Button(L("clearSection"), false); clear.Height = 32; clear.Margin = new Thickness(8, 0, 0, 0); DockPanel.SetDock(clear, Dock.Right); toolsRow.Children.Add(clear);
        panel.Children.Add(toolsRow);
        var count = new TextBlock { FontSize = 10, Foreground = Brush("muted"), Margin = new Thickness(2, 0, 0, 5) }; panel.Children.Add(count);
        var rowList = new List<(OptimizationOption option, FrameworkElement row, CheckBox check)>();
        var list = new StackPanel();
        foreach (var group in options.GroupBy(option => option.Group))
        {
            var title = new TextBlock { Text = GroupTitle(group.Key), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brush("muted"), Margin = new Thickness(2, 15, 0, 4) };
            list.Children.Add(title);
            foreach (var option in group)
            {
                var row = new Border { BorderBrush = Brush("line"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(2, 8, 4, 8) };
                var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var body = new StackPanel();
                var risk = OptionRisk(option);
                var displayLabel = OptionDisplayLabel(option);
                var check = new ConfirmingCheckBox { IsChecked = GetFlag(option.Id), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Brush("text"), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = LocalizedOptionRisk(option), Template = ToggleSwitchTemplate(risk == "critical") };
                if (risk == "critical") check.ConfirmBeforeCheck = () => MessageBox.Show(this, L("confirmOptimization") + "\n\n" + displayLabel + "\n" + OptionTarget(option), L("confirmTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) == MessageBoxResult.OK;
                check.Content = new TextBlock { Text = displayLabel, TextWrapping = TextWrapping.Wrap, MaxWidth = 405 };
                System.Windows.Automation.AutomationProperties.SetName(check, displayLabel);
                check.Checked += (_, _) => OnOptionCheck(option, check, true, count, options);
                check.Unchecked += (_, _) => OnOptionCheck(option, check, false, count, options);
                body.Children.Add(check);
                body.Children.Add(new TextBlock { Text = OptionTarget(option), FontSize = 10, Foreground = Brush("muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(58, 2, 4, 0) });
                if (option.Scope == "sys") body.Children.Add(new TextBlock { Text = L("targetScopeSystem"), FontSize = 9, Foreground = Brush("muted"), Margin = new Thickness(58, 2, 0, 0) });
                else body.Children.Add(new TextBlock { Text = L("targetScopeUser"), FontSize = 9, Foreground = Brush("muted"), Margin = new Thickness(58, 2, 0, 0) });
                Grid.SetColumn(body, 0); grid.Children.Add(body);
                if (risk != "")
                {
                    var badge = new Border { Background = Brush(risk == "critical" ? "dangerSoft" : "warningSoft"), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
                    badge.Child = new TextBlock { Text = L(risk), FontSize = 8, FontWeight = FontWeights.Bold, Foreground = Brush(risk == "critical" ? "danger" : "warning") };
                    Grid.SetColumn(badge, 1); grid.Children.Add(badge);
                }
                row.Child = grid; list.Children.Add(row); rowList.Add((option, row, check));
            }
        }
        panel.Children.Add(list);
        void RefreshCount() => count.Text = $"{rowList.Count(item => GetFlag(item.option.Id))} {L("selectedCount")}  ·  {options.Count} {L("availableCount")}";
        RefreshCount();
        filter.TextChanged += (_, _) =>
        {
            var query = filter.Text.Trim();
            foreach (var item in rowList) item.row.Visibility = string.IsNullOrEmpty(query) || (OptionDisplayLabel(item.option) + " " + item.option.Label + " " + OptionTarget(item.option) + " " + item.option.Id).Contains(query, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        };
        clear.Click += (_, _) =>
        {
            updatingOptions = true;
            foreach (var item in rowList) { state.Flags[item.option.Id] = false; item.check.IsChecked = false; }
            updatingOptions = false; RefreshCount(); UpdatePreview();
        };
    }

    private void OnOptionCheck(OptimizationOption option, CheckBox check, bool value, TextBlock? count, IReadOnlyList<OptimizationOption> sectionOptions)
    {
        state.Flags[option.Id] = value;
        if (!updatingOptions) UpdatePreview();
        if (count is not null) count.Text = $"{sectionOptions.Count(item => GetFlag(item.Id))} {L("selectedCount")}  ·  {sectionOptions.Count} {L("availableCount")}";
    }

    private string GroupTitle(string group) => group switch
    {
        "privacy" => L("optionsPrivacy"), "notif" => L("optionsNotifications"), "sound" => L("optionsSound"),
        "perf" => L("optionsPerf"), "power-plan" or "powerreg" => L("optionsPower"), "power-settings" => L("optionsPowerSettings"),
        "explorer" => L("optionsExplorer"), "start" => L("optionsStart"), "taskbar" => L("optionsTaskbar"), "theme" => L("optionsTheme"),
        "apps-packages" => L("optionsPackages"), "apps-caps" => L("optionsCapabilities"), "apps-scripts" or "usertask" => L("optionsAdvanced"),
        "services" => L("optionsServices"), "tasks" => L("optionsTasks"), "wupdate" => L("optionsUpdates"), _ => group.ToUpperInvariant()
    };

    private string OptionDisplayLabel(OptimizationOption option)
    {
        if (option.Action == "AppxPackage")
        {
            var friendly = option.Package switch
            {
                "Microsoft.Microsoft3DViewer" => "3D Viewer", "Microsoft.MixedReality.Portal" => "Mixed Reality Portal", "Microsoft.BingSearch" => "Pesquisa do Bing / Search", "Microsoft.BingNews" => "Microsoft Notícias", "Microsoft.BingWeather" => "Clima", "Microsoft.WindowsCamera" => "Câmera", "Clipchamp.Clipchamp" => "Clipchamp", "Microsoft.WindowsAlarms" => "Relógio", "Microsoft.549981C3F5F10" or "Microsoft.Copilot" or "Microsoft.Windows.Ai.Copilot.Provider" or "Microsoft.Copilot_8wekyb3d8bbwe" => "Microsoft Copilot (IA)", "Microsoft.GetHelp" => "Obter Ajuda", "Microsoft.Windows.DevHome" => "Dev Home", "MicrosoftCorporationII.MicrosoftFamily" => "Microsoft Family", "microsoft.windowscommunicationsapps" => "Email e Calendário", "Microsoft.SkypeApp" => "Skype", "MSTeams" => "Microsoft Teams", "Microsoft.WindowsFeedbackHub" => "Hub de Comentários", "Microsoft.WindowsMaps" => "Mapas", "Microsoft.MicrosoftOfficeHub" => "Microsoft 365 / Office", "Microsoft.OutlookForWindows" => "Outlook", "Microsoft.MSPaint" => "Paint", "Microsoft.Windows.Photos" => "Fotos", "Microsoft.People" => "Pessoas", "Microsoft.PowerAutomateDesktop" => "Power Automate", "MicrosoftCorporationII.QuickAssist" => "Assistência Rápida", "Microsoft.MicrosoftSolitaireCollection" => "Microsoft Solitaire", "Microsoft.GamingApp" => "Xbox", "Microsoft.XboxApp" => "Xbox", "Microsoft.XboxIdentityProvider" => "Xbox Identity Provider", "Microsoft.XboxGameOverlay" or "Microsoft.XboxGamingOverlay" => "Xbox Game Bar", "Microsoft.Xbox.TCUI" => "Xbox TCUI", "Microsoft.WindowsStore" => "Microsoft Store", "Microsoft.ZuneMusic" => "Media Player / Groove Música", "Microsoft.ZuneVideo" => "Filmes e TV", "Microsoft.WindowsSoundRecorder" => "Gravador de Som", "Microsoft.MicrosoftStickyNotes" => "Sticky Notes", "Microsoft.Getstarted" => "Dicas", "Microsoft.Todos" => "Microsoft To Do", "Microsoft.YourPhone" => "Vincular ao Celular", "Microsoft.Office.OneNote" => "OneNote", _ => option.Package ?? option.Label
            };
            return L("removePrefix") + " " + friendly;
        }
        return LocalizeCatalogLabel(option.Label);
    }

    private string LocalizedOptionRisk(OptimizationOption option) => string.IsNullOrWhiteSpace(option.Risk) ? option.Label : option.Risk.StartsWith("O Edge e o WebView2", StringComparison.Ordinal) ? L("edgeRemovalRisk") : option.Risk;

    private string OptionTarget(OptimizationOption option) => option.Action switch
    {
        "SetRegistry" => $"{option.Path}\\{option.Name}  →  {option.RegistryType} = {option.Value}",
        "RemoveValue" => $"{L("removeRegistryValue")}: {option.Path}\\{option.Name}",
        "RemoveKey" => $"{L("removeRegistryKey")}: {option.Path}",
        "NewKey" => $"{L("createRegistryKey")}: {option.Path}",
        "BinaryBit" => $"{L("binaryFlag")}: {option.Path}\\{option.Name} · byte {option.ByteIndex} · mask {option.BitMask}",
        "BinaryByte" => $"{L("binaryValue")}: {option.Path}\\{option.Name} · byte {option.ByteIndex} = {option.Value}",
        "AppxPackage" => $"{L("removeAppxPackage")}: {option.Package}",
        "Capability" => $"{L("removeCapability")}: {option.Package}",
        "ScheduledTask" => $"{L("disableTask")}: {option.TaskName}",
        "PowerSetting" => $"{L("acPower")}: {option.Ac}  ·  {L("batteryPower")}: {option.Dc}",
        "PowerPlan" => L("targetPowerPlan"),
        "HibernateOff" => "powercfg /hibernate off",
        "EdgeRemoval" => L("targetEdgeRemoval"),
        "OneDriveRemoval" => L("targetOneDriveRemoval"),
        "CleanStart" => L("targetCleanStart"),
        "RebootAfter" => L("targetRebootAfter"),
        _ => option.Action
    };

    private string OptionRisk(OptimizationOption option)
    {
        if (option.Action is "EdgeRemoval" or "HibernateOff" or "RebootAfter" || option.Package == "Microsoft.WindowsStore") return "critical";
        if (option.Action is "OneDriveRemoval" or "PowerPlan" || option.Group is "services" or "wupdate" or "power-settings" || option.Action is "Capability" or "AppxPackage" or "ScheduledTask") return "caution";
        if (option.Id.Contains("tcp", StringComparison.OrdinalIgnoreCase) || option.Id.Contains("network", StringComparison.OrdinalIgnoreCase)) return "caution";
        if (!string.IsNullOrWhiteSpace(option.Risk)) return "caution";
        return "";
    }

    private void BuildPost(Panel panel)
    {
        panel.Children.Add(new TextBlock { Text = L("postSecurity"), FontSize = 11, Foreground = Brush("warning"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = L("wingetHint"), FontSize = 11, Foreground = Brush("warning"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = L("popularPrograms"), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brush("muted"), Margin = new Thickness(2, 6, 0, 5) });
        foreach (var app in apps) panel.Children.Add(Check("install:" + app.Id, app.Name));
        panel.Children.Add(new TextBlock { Text = L("customIds"), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brush("text"), Margin = new Thickness(1, 16, 0, 5) });
        var ids = new TextBox { Text = GetField("customWingetIds", ""), Height = 80, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = StudioFont, FontSize = 11, Background = Brush("surface"), Foreground = Brush("text"), BorderBrush = Brush("line"), Padding = new Thickness(8) };
        ids.TextChanged += (_, _) => { SetField("customWingetIds", ids.Text); UpdatePreview(); }; panel.Children.Add(ids);
        panel.Children.Add(Check("autoRun", L("autoRun"), false)); panel.Children.Add(Check("selfDelete", L("selfDelete"), false));
        panel.Children.Add(Check("createRestore", L("createRestore"), false));
        panel.Children.Add(new TextBlock { Text = L("restoreHint"), FontSize = 10, Foreground = Brush("muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28, 2, 0, 8) });
    }

    private void AddChecks(Panel panel, params (string key, string label)[] values) { foreach (var (key, label) in values) panel.Children.Add(Check(key, label)); }
    private string LocalCategory(string key) => key switch
    {
        "Navegador" => state.Language == "en-US" ? "BROWSERS" : state.Language == "es-ES" ? "NAVEGADORES" : "NAVEGADORES",
        "Utilitários" => state.Language == "en-US" ? "UTILITIES" : state.Language == "es-ES" ? "UTILIDADES" : "UTILITÁRIOS",
        "Multimídia" => state.Language == "en-US" ? "MEDIA" : state.Language == "es-ES" ? "MULTIMEDIA" : "MULTIMÍDIA",
        "Comunicação" => state.Language == "en-US" ? "COMMUNICATION" : state.Language == "es-ES" ? "COMUNICACIÓN" : "COMUNICAÇÃO",
        "Desenvolvimento" => state.Language == "en-US" ? "DEVELOPMENT" : state.Language == "es-ES" ? "DESARROLLO" : "DESENVOLVIMENTO",
        "Jogos" => state.Language == "en-US" ? "GAMES" : state.Language == "es-ES" ? "JUEGOS" : "JOGOS",
        "Segurança" => state.Language == "en-US" ? "SECURITY" : state.Language == "es-ES" ? "SEGURIDAD" : "SEGURANÇA",
        "Microsoft" => "MICROSOFT",
        "Xbox" => "XBOX",
        _ => key.ToUpperInvariant()
    };
    private CheckBox Check(string key, string label, bool danger = false, string? tooltip = null)
    {
        var check = new ConfirmingCheckBox { Content = label, IsChecked = GetFlag(key), Margin = new Thickness(1, 5, 0, 5), FontSize = 12, Foreground = Brush(danger ? "danger" : "text"), Background = Brush("surface"), BorderBrush = Brush("muted"), ToolTip = tooltip, VerticalContentAlignment = VerticalAlignment.Center, Template = ToggleSwitchTemplate(danger) };
        if (key == "wipeDisk") check.ConfirmBeforeCheck = () => MessageBox.Show(this, L("confirmWipe").Replace("{disk}", GetField("targetDisk", "0")) + "\n\n" + L("continue"), L("confirmTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) == MessageBoxResult.OK;
        check.Checked += (_, _) => OnCheck(key, true); check.Unchecked += (_, _) => OnCheck(key, false); return check;
    }
    private void OnCheck(string key, bool value)
    {
        state.Flags[key] = value; UpdatePreview();
    }
    private TextBox Field(Panel panel, string key, string label, string fallback, bool secret)
    {
        var box = new TextBox { Text = GetField(key, fallback), Height = 34, FontSize = 12, Background = Brush("surface"), Foreground = Brush("text"), BorderBrush = Brush("line"), Padding = new Thickness(8, 6, 8, 5), Margin = new Thickness(0, 0, 0, 9), ToolTip = secret ? "Password is stored as readable text" : null };
        if (secret) { var p = new PasswordBox { Password = box.Text, Height = 34, FontSize = 12, Background = Brush("surface"), Foreground = Brush("text"), BorderBrush = Brush("line"), Padding = new Thickness(8, 5, 8, 4), Margin = new Thickness(0, 0, 0, 9) }; p.PasswordChanged += (_, _) => { SetField(key, p.Password); UpdatePreview(); }; AddLabel(panel, label); panel.Children.Add(p); return box; }
        box.TextChanged += (_, _) => { SetField(key, box.Text); UpdatePreview(); }; AddLabel(panel, label); panel.Children.Add(box); return box;
    }
    private void AddLabel(Panel panel, string label) => panel.Children.Add(new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brush("text"), Margin = new Thickness(1, 8, 0, 5) });
    private void StyleCombo(ComboBox combo, string background, string foreground, string border)
    {
        combo.Background = Brush(background);
        combo.Foreground = Brush(foreground);
        combo.BorderBrush = Brush(border);
        combo.FontFamily = StudioFont;
        combo.Padding = new Thickness(8, 3, 6, 3);
        combo.ItemContainerStyle = new Style(typeof(ComboBoxItem))
        {
            Setters =
            {
                new Setter(ComboBoxItem.BackgroundProperty, Brush(background)),
                new Setter(ComboBoxItem.ForegroundProperty, Brush(foreground)),
                new Setter(ComboBoxItem.PaddingProperty, new Thickness(8, 5, 8, 5))
            },
            Triggers =
            {
                new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true, Setters = { new Setter(ComboBoxItem.BackgroundProperty, Brush("selected")) } },
                new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true, Setters = { new Setter(ComboBoxItem.BackgroundProperty, Brush("selected")) } }
            }
        };
        combo.Template = ComboTemplate();
    }
    private ControlTemplate ComboTemplate()
    {
        var frame = new FrameworkElementFactory(typeof(Border));
        frame.Name = "ComboFrame";
        frame.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Control.Background)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        frame.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(Control.BorderBrush)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        frame.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(Control.BorderThickness)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        var layout = new FrameworkElementFactory(typeof(DockPanel));
        layout.SetValue(DockPanel.LastChildFillProperty, true);
        var toggle = new FrameworkElementFactory(typeof(ToggleButton));
        toggle.SetValue(DockPanel.DockProperty, Dock.Right);
        toggle.SetValue(FrameworkElement.WidthProperty, 30d);
        toggle.SetValue(ToggleButton.ContentProperty, "⌄");
        toggle.SetValue(Control.FontFamilyProperty, StudioFont);
        toggle.SetValue(Control.FontSizeProperty, 12d);
        toggle.SetValue(Control.ForegroundProperty, Brush("muted"));
        toggle.SetValue(Control.BackgroundProperty, Brushes.Transparent);
        toggle.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        toggle.SetValue(Control.FocusableProperty, false);
        toggle.SetBinding(ToggleButton.IsCheckedProperty, new System.Windows.Data.Binding(nameof(ComboBox.IsDropDownOpen)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Mode = System.Windows.Data.BindingMode.TwoWay });
        layout.AppendChild(toggle);
        var selected = new FrameworkElementFactory(typeof(ContentPresenter));
        selected.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 2, 4, 2));
        selected.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        selected.SetValue(UIElement.IsHitTestVisibleProperty, false);
        selected.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding(nameof(ComboBox.SelectionBoxItem)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        layout.AppendChild(selected);
        var popupFrame = new FrameworkElementFactory(typeof(Border));
        popupFrame.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Control.Background)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        popupFrame.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(Control.BorderBrush)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        popupFrame.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var items = new FrameworkElementFactory(typeof(ItemsPresenter));
        items.SetValue(System.Windows.Input.KeyboardNavigation.DirectionalNavigationProperty, System.Windows.Input.KeyboardNavigationMode.Contained);
        var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
        scroll.SetValue(ScrollViewer.CanContentScrollProperty, true);
        scroll.SetValue(ScrollViewer.MaxHeightProperty, 320d);
        scroll.AppendChild(items);
        popupFrame.AppendChild(scroll);
        var popup = new FrameworkElementFactory(typeof(Popup));
        popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
        popup.SetValue(Popup.AllowsTransparencyProperty, true);
        popup.SetValue(Popup.FocusableProperty, false);
        popup.SetValue(Popup.StaysOpenProperty, false);
        popup.SetBinding(Popup.IsOpenProperty, new System.Windows.Data.Binding(nameof(ComboBox.IsDropDownOpen)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent), Mode = System.Windows.Data.BindingMode.TwoWay });
        popup.SetBinding(Popup.PlacementTargetProperty, new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        popup.AppendChild(popupFrame);
        layout.AppendChild(popup);
        frame.AppendChild(layout);
        var template = new ControlTemplate(typeof(ComboBox)) { VisualTree = frame };
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("accent"), "ComboFrame"));
        focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1), "ComboFrame"));
        template.Triggers.Add(focus);
        return template;
    }
    private void AddLabeled(Grid grid, int col, string label, Control control)
    {
        var box = new StackPanel { Margin = new Thickness(col == 0 ? 0 : 8, 0, col == 0 ? 8 : 0, 0) }; AddLabel(box, label); box.Children.Add(control); Grid.SetColumn(box, col); grid.Children.Add(box);
    }
    private Button Button(string text, bool primary)
    {
        return new Button { Content = text, Height = 34, Padding = new Thickness(13, 0, 13, 0), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = primary ? Brush("accentInk") : Brush("text"), Background = primary ? Brush("accent") : Brush("surface"), BorderBrush = primary ? Brush("accent") : Brush("line"), BorderThickness = new Thickness(1), Cursor = System.Windows.Input.Cursors.Hand, Template = GlassButtonTemplate(false) };
    }
    private ControlTemplate FlatButtonTemplate() => GlassButtonTemplate(true);

    private ControlTemplate GlassButtonTemplate(bool navigation)
    {
        var radius = navigation ? new CornerRadius(12, 0, 0, 12) : new CornerRadius(5);
        var frame = new FrameworkElementFactory(typeof(Border));
        frame.Name = "GlassFrame";
        frame.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Control.Background)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        frame.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(Control.BorderBrush)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        frame.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(Control.BorderThickness)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        frame.SetValue(Border.CornerRadiusProperty, radius);

        var layers = new FrameworkElementFactory(typeof(Grid));
        var wash = new FrameworkElementFactory(typeof(Border));
        wash.Name = "GlassWash";
        wash.SetValue(Border.BackgroundProperty, Brush("glassWash"));
        wash.SetValue(Border.CornerRadiusProperty, radius);
        wash.SetValue(UIElement.OpacityProperty, 0d);
        wash.SetValue(UIElement.IsHitTestVisibleProperty, false);
        layers.AppendChild(wash);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, navigation ? HorizontalAlignment.Stretch : HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetBinding(FrameworkElement.MarginProperty, new System.Windows.Data.Binding(nameof(Control.Padding)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        layers.AppendChild(content);
        if (navigation)
        {
            var liquid = new FrameworkElementFactory(typeof(Border));
            liquid.Name = "GlassSheen";
            liquid.SetValue(FrameworkElement.WidthProperty, 228d);
            liquid.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            liquid.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            liquid.SetValue(Border.BackgroundProperty, GlassRibbonBrush());
            liquid.SetValue(Border.CornerRadiusProperty, radius);
            liquid.SetValue(UIElement.OpacityProperty, 0d);
            liquid.SetValue(UIElement.EffectProperty, new System.Windows.Media.Effects.BlurEffect { Radius = 8, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance });
            liquid.SetValue(UIElement.RenderTransformProperty, new TranslateTransform(-228, 0));
            liquid.SetValue(UIElement.IsHitTestVisibleProperty, false);
            layers.AppendChild(liquid);
        }
        else
        {
            var sheen = new FrameworkElementFactory(typeof(Border));
            sheen.Name = "GlassSheen";
            sheen.SetValue(Border.BackgroundProperty, Brush("glassSheen"));
            sheen.SetValue(Border.HeightProperty, 1d);
            sheen.SetValue(Border.MarginProperty, new Thickness(8, 2, 8, 0));
            sheen.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Top);
            sheen.SetValue(UIElement.OpacityProperty, 0d);
            sheen.SetValue(UIElement.IsHitTestVisibleProperty, false);
            layers.AppendChild(sheen);
        }
        frame.AppendChild(layers);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = frame };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.EnterActions.Add(FadeStoryboard("GlassWash", .62, 145));
        hover.ExitActions.Add(FadeStoryboard("GlassWash", 0, 125));
        if (navigation)
        {
            hover.EnterActions.Add(FadeStoryboard("GlassSheen", .74, 80));
            hover.EnterActions.Add(GlassSweepStoryboard());
            hover.ExitActions.Add(FadeStoryboard("GlassSheen", 0, 170));
        }
        else
        {
            hover.EnterActions.Add(FadeStoryboard("GlassSheen", .58, 135));
            hover.ExitActions.Add(FadeStoryboard("GlassSheen", 0, 115));
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("glassEdge"), "GlassFrame"));
            hover.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1), "GlassFrame"));
        }
        template.Triggers.Add(hover);

        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        if (navigation)
            focus.Setters.Add(new Setter(UIElement.OpacityProperty, .82, "GlassWash"));
        else
        {
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("accent"), "GlassFrame"));
            focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1), "GlassFrame"));
        }
        template.Triggers.Add(focus);

        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, .84));
        template.Triggers.Add(pressed);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .48));
        template.Triggers.Add(disabled);
        return template;
    }

    private static BeginStoryboard FadeStoryboard(string target, double opacity, int milliseconds)
    {
        var animation = new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTargetName(animation, target);
        Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        return new BeginStoryboard { Storyboard = storyboard };
    }

    private Brush GlassRibbonBrush()
    {
        var color = state.Dark ? Colors.White : Color.FromRgb(255, 255, 255);
        var ribbon = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
        ribbon.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 0));
        ribbon.GradientStops.Add(new GradientStop(Color.FromArgb(17, color.R, color.G, color.B), .28));
        ribbon.GradientStops.Add(new GradientStop(Color.FromArgb(30, color.R, color.G, color.B), .5));
        ribbon.GradientStops.Add(new GradientStop(Color.FromArgb(17, color.R, color.G, color.B), .72));
        ribbon.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        return ribbon;
    }

    private static BeginStoryboard GlassSweepStoryboard()
    {
        var animation = new DoubleAnimation(-228, 0, TimeSpan.FromMilliseconds(430))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTargetName(animation, "GlassSheen");
        Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        return new BeginStoryboard { Storyboard = storyboard };
    }

    private ControlTemplate ToggleSwitchTemplate(bool danger)
    {
        var activeBrush = Brush(danger ? "danger" : "switchActive");
        var frame = new FrameworkElementFactory(typeof(DockPanel));
        frame.SetValue(DockPanel.LastChildFillProperty, true);
        var track = new FrameworkElementFactory(typeof(Border));
        track.Name = "SwitchTrack";
        track.SetValue(DockPanel.DockProperty, Dock.Left);
        track.SetValue(FrameworkElement.WidthProperty, 48d);
        track.SetValue(FrameworkElement.HeightProperty, 26d);
        track.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
        track.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
        track.SetValue(Border.BackgroundProperty, Brush("switchOff"));
        track.SetValue(Border.BorderBrushProperty, Brush("switchLine"));
        track.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        var trackLayers = new FrameworkElementFactory(typeof(Grid));
        var thumb = new FrameworkElementFactory(typeof(Border));
        thumb.Name = "SwitchThumb";
        thumb.SetValue(FrameworkElement.WidthProperty, 20d);
        thumb.SetValue(FrameworkElement.HeightProperty, 20d);
        thumb.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        thumb.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        thumb.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 2, 0));
        thumb.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        thumb.SetValue(Border.BackgroundProperty, LiquidGlassThumbBrush());
        thumb.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(41, 255, 255, 255)));
        thumb.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        thumb.SetValue(UIElement.EffectProperty, new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 3, ShadowDepth = 1, Opacity = .16 });
        thumb.SetValue(UIElement.RenderTransformProperty, new TranslateTransform());

        var lip = new FrameworkElementFactory(typeof(Border));
        lip.Name = "SwitchGlassLip";
        lip.SetValue(FrameworkElement.WidthProperty, 15d);
        lip.SetValue(FrameworkElement.HeightProperty, 15d);
        lip.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        lip.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        lip.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        lip.SetValue(Border.BackgroundProperty, LiquidGlassLipBrush());
        lip.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(41, 255, 255, 255)));
        lip.SetValue(Border.BorderThicknessProperty, new Thickness(.6));
        lip.SetValue(UIElement.RenderTransformProperty, new ScaleTransform(1.10, 1.10));
        lip.SetValue(UIElement.EffectProperty, new System.Windows.Media.Effects.BlurEffect { Radius = .35, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance });
        lip.SetValue(UIElement.IsHitTestVisibleProperty, false);
        thumb.AppendChild(lip);
        trackLayers.AppendChild(thumb);
        track.AppendChild(trackLayers);
        frame.AppendChild(track);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.ContentSourceProperty, "Content");
        content.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 2, 1));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        frame.AppendChild(content);

        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = frame };
        var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        on.Setters.Add(new Setter(Border.BackgroundProperty, activeBrush, "SwitchTrack"));
        on.Setters.Add(new Setter(Border.BorderBrushProperty, activeBrush, "SwitchTrack"));
        on.EnterActions.Add(TranslateThumbStoryboard(22));
        on.ExitActions.Add(TranslateThumbStoryboard(0));
        template.Triggers.Add(on);

        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, Brush("accent"), "SwitchTrack"));
        focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(2), "SwitchTrack"));
        template.Triggers.Add(focus);

        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .5));
        template.Triggers.Add(disabled);
        return template;
    }

    // The translucent highlight sits over the track; the shallow blur keeps the
    // glass lens readable at the switch's compact size.
    private Brush LiquidGlassThumbBrush()
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(.5, .5),
            GradientOrigin = new Point(.32, .25),
            RadiusX = .76,
            RadiusY = .76,
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(105, 255, 255, 255), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(128, 255, 255, 255), .56));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(178, 255, 255, 255), .76));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(230, 255, 255, 255), .9));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(154, 255, 255, 255), 1));
        return brush;
    }

    private Brush LiquidGlassLipBrush()
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(.5, .5),
            GradientOrigin = new Point(.3, .24),
            RadiusX = .78,
            RadiusY = .78,
            MappingMode = BrushMappingMode.RelativeToBoundingBox
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(18, 255, 255, 255), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(12, 255, 255, 255), .62));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(41, 255, 255, 255), .86));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(20, 255, 255, 255), 1));
        return brush;
    }

    private static BeginStoryboard TranslateThumbStoryboard(double x)
    {
        var animation = new DoubleAnimation(x, TimeSpan.FromMilliseconds(190))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTargetName(animation, "SwitchThumb");
        Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        return new BeginStoryboard { Storyboard = storyboard };
    }
    private string GetField(string key, string fallback) => state.Fields.TryGetValue(key, out var val) ? val : fallback;
    private void SetField(string key, string value) => state.Fields[key] = value;
    private bool GetFlag(string key) => state.Flags.TryGetValue(key, out var val) ? val : key is "unattended" or "dynamicUpdate";
    private Brush Brush(string key)
    {
        var colors = state.Dark
            ? new Dictionary<string, string> { ["bg"]="#171918", ["side"]="#202321", ["sideSelected"]="#2B302D", ["surface"]="#171918", ["previewBg"]="#202321", ["previewControl"]="#292D2A", ["previewLine"]="#3A3E3B", ["previewText"]="#E9E6DB", ["previewMuted"]="#A6B4A8", ["previewAccent"]="#F0D277", ["sideControl"]="#292D2A", ["sideLine"]="#454A46", ["sideText"]="#E9E6DB", ["sideMuted"]="#A6B4A8", ["sideAccent"]="#F0D277", ["line"]="#343835", ["text"]="#E9E6DB", ["muted"]="#9CAAA0", ["accent"]="#E8C34F", ["switchActive"]="#A8843D", ["accentInk"]="#171918", ["code"]="#E0DED5", ["warning"]="#D1A267", ["danger"]="#EF9183", ["selected"]="#292D2A", ["glassWash"]="#1FE8C34F", ["glassEdge"]="#48E8C34F", ["glassSheen"]="#6FFFFFFF", ["switchOff"]="#343835", ["switchLine"]="#4B514D", ["switchThumb"]="#E9E6DB", ["switchThumbLine"]="#A6B4A8", ["switchShine"]="#BFFFFFFF", ["warningSoft"]="#393127", ["dangerSoft"]="#3C2928" }
            : new Dictionary<string, string> { ["bg"]="#F4F5F6", ["side"]="#E8ECEE", ["sideSelected"]="#DDE6EC", ["surface"]="#F4F5F6", ["previewBg"]="#E8EDF1", ["previewControl"]="#DDE5EB", ["previewLine"]="#C7D2DA", ["previewText"]="#242B31", ["previewMuted"]="#526372", ["previewAccent"]="#275985", ["sideControl"]="#DDE5EB", ["sideLine"]="#BAC7D1", ["sideText"]="#242B31", ["sideMuted"]="#526372", ["sideAccent"]="#275985", ["line"]="#CDD6DE", ["text"]="#242B31", ["muted"]="#526372", ["accent"]="#275985", ["switchActive"]="#275985", ["accentInk"]="#FFFFFF", ["code"]="#344C61", ["warning"]="#79531D", ["danger"]="#A6323A", ["selected"]="#DDE6EC", ["glassWash"]="#22275985", ["glassEdge"]="#80275985", ["glassSheen"]="#99FFFFFF", ["switchOff"]="#DDE5EB", ["switchLine"]="#BAC7D1", ["switchThumb"]="#FFFFFF", ["switchThumbLine"]="#CDD6DE", ["switchShine"]="#E8FFFFFF", ["warningSoft"]="#EFE8DA", ["dangerSoft"]="#F3E3E3" };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[key])!);
    }

    private void ApplyPreset(int choice)
    {
        state.Flags.Clear(); state.Fields.Clear();
        state.Flags["unattended"] = true; state.Flags["interface1"] = true; SetField("locale", "pt-BR");
        if (choice == 2)
        {
            foreach (var option in catalog.Where(x => x.Group == "privacy" && x.Risk is null)) state.Flags[option.Id] = true;
            foreach (var id in new[] { "Microsoft.VisualStudioCode", "Git.Git", "7zip.7zip" }) state.Flags["install:" + id] = true;
        }
        if (choice == 3)
        {
            foreach (var option in catalog.Where(x => x.Group == "perf" && x.Risk is null)) state.Flags[option.Id] = true;
            foreach (var id in new[] { "Valve.Steam", "Discord.Discord", "Google.Chrome", "VideoLAN.VLC" }) state.Flags["install:" + id] = true;
        }
        if (choice == 4)
            foreach (var option in catalog.Where(x => x.Group is "privacy" or "notif" or "sound")) state.Flags[option.Id] = true;
        if (choice == 5)
            foreach (var option in catalog.Where(x => x.Group is "perf" or "power-settings" or "powerreg" or "power-plan" && x.Risk is null)) state.Flags[option.Id] = true;
        status.Text = L("preset") + ": " + (choice == 1 ? L("starter") : choice == 2 ? L("dev") : choice == 3 ? L("gaming") : choice == 4 ? L("privacyPreset") : L("performancePreset"));
    }

    private void UpdatePreview()
    {
        if (preview is null) return;
        try { preview.Text = previewMode == "xml" ? GenerateXml().ToString(SaveOptions.None) : BuildPostScript(); } catch { }
    }

    private XDocument GenerateXml()
    {
        XNamespace un = "urn:schemas-microsoft-com:unattend", wcm = "http://schemas.microsoft.com/WMIConfig/2002/State";
        var root = new XElement(un + "unattend", new XAttribute("xmlns", un.NamespaceName), new XAttribute(XNamespace.Xmlns + "wcm", wcm));
        var locale = GetField("locale", "pt-BR"); var keyboard = GetField("keyboard", "Português ABNT2");
        var input = keyboard switch { "US" => "0409:00000409", "Español" => "0c0a:0000040a", _ => "0416:00010416" };
        var winpe = Component(un, "Microsoft-Windows-International-Core-WinPE");
        winpe.Add(new XElement(un+"SetupUILanguage", new XElement(un+"UILanguage", locale)), new XElement(un+"InputLocale", input), new XElement(un+"SystemLocale", locale), new XElement(un+"UILanguage", locale), new XElement(un+"UserLocale", locale));
        var setup = Component(un, "Microsoft-Windows-Setup");
        if (GetFlag("wipeDisk")) setup.Add(BuildDisk(un, wcm, TargetDiskId()));
        var imageMode = GetField("imageSelectionMode", "edition");
        var imageKey = imageMode switch { "index" => "/IMAGE/INDEX", "description" => "/IMAGE/DESCRIPTION", _ => "/IMAGE/NAME" };
        var imageValue = imageMode == "edition" ? $"Windows {GetField("os", "Windows 11").Replace("Windows ", "")} {GetField("edition", "Pro")}" : GetField("imageSelectorValue", "").Trim();
        var osImage = new XElement(un+"OSImage", new XElement(un+"InstallFrom", new XElement(un+"MetaData", new XAttribute(wcm+"action", "add"), new XElement(un+"Key", imageKey), new XElement(un+"Value", imageValue))));
        if (GetFlag("wipeDisk")) osImage.Add(new XElement(un+"InstallTo", new XElement(un+"DiskID", TargetDiskId()), new XElement(un+"PartitionID", "3")));
        else osImage.Add(new XElement(un+"InstallToAvailablePartition", "true"));
        setup.Add(new XElement(un+"ImageInstall", osImage));
        setup.Add(new XElement(un+"DynamicUpdate", new XElement(un+"Enable", GetFlag("dynamicUpdate").ToString().ToLowerInvariant()), new XElement(un+"WillShowUI", "Never")));
        var userData = new XElement(un+"UserData", new XElement(un+"AcceptEula", "true"));
        var fullName = GetField("fullName", "").Trim(); var organization = GetField("organization", "").Trim();
        if (fullName.Length > 0) userData.Add(new XElement(un+"FullName", fullName));
        if (organization.Length > 0) userData.Add(new XElement(un+"Organization", organization));
        setup.Add(userData);
        var windowsPeSettings = new XElement(un+"settings", new XAttribute("pass", "windowsPE"), winpe, setup);
        root.Add(windowsPeSettings);
        var specialize = Component(un, "Microsoft-Windows-Shell-Setup");
        var computer = GetField("computer", "").Trim(); if (!string.IsNullOrWhiteSpace(computer)) specialize.Add(new XElement(un+"ComputerName", Regex.Replace(computer, "[^A-Za-z0-9-]", "")[..Math.Min(15, Regex.Replace(computer, "[^A-Za-z0-9-]", "").Length)]));
        specialize.Add(new XElement(un+"TimeZone", GetField("timezone", "E. South America Standard Time"))); root.Add(new XElement(un+"settings", new XAttribute("pass", "specialize"), specialize));
        var oobe = Component(un, "Microsoft-Windows-Shell-Setup");
        oobe.Add(new XElement(un+"OOBE", new XElement(un+"HideEULAPage", "true"), new XElement(un+"HideOnlineAccountScreens", "true"), new XElement(un+"HideWirelessSetupInOOBE", "true"), new XElement(un+"ProtectYourPC", "3")));
        if (GetFlag("account"))
        {
            var user = GetField("username", "").Trim(); var pass = GetField("password", "");
            oobe.Add(new XElement(un+"UserAccounts", new XElement(un+"LocalAccounts", new XElement(un+"LocalAccount", new XAttribute(wcm+"action", "add"), new XElement(un+"Name", user), new XElement(un+"Group", GetField("accountType", "Administrators")), new XElement(un+"Password", new XElement(un+"Value", pass), new XElement(un+"PlainText", "true"))))));
        }
        var script = BuildPostScript();
        if (!string.IsNullOrWhiteSpace(script))
        {
            var command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -Command \"" + BuildBootstrap(script) + "\"";
            oobe.Add(new XElement(un+"FirstLogonCommands", new XElement(un+"SynchronousCommand", new XAttribute(wcm+"action", "add"), new XElement(un+"Order", "1"), new XElement(un+"Description", "Create post-install script on Desktop"), new XElement(un+"CommandLine", command))));
        }
        root.Add(new XElement(un+"settings", new XAttribute("pass", "oobeSystem"), oobe));
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root); return document;
    }
    private static XElement Component(XNamespace un, string name) => new(un+"component", new XAttribute("name", name), new XAttribute("processorArchitecture", "amd64"), new XAttribute("publicKeyToken", "31bf3856ad364e35"), new XAttribute("language", "neutral"), new XAttribute("versionScope", "nonSxS"));
    private uint TargetDiskId() => uint.TryParse(GetField("targetDisk", "0"), out var disk) ? disk : 0;
    private XElement BuildDisk(XNamespace un, XNamespace wcm, uint diskId)
    {
        return new XElement(un+"DiskConfiguration", new XElement(un+"Disk", new XAttribute(wcm+"action", "add"), new XElement(un+"DiskID", diskId), new XElement(un+"WillWipeDisk", "true"),
            new XElement(un+"CreatePartitions", new XElement(un+"CreatePartition", new XAttribute(wcm+"action", "add"), new XElement(un+"Order", "1"), new XElement(un+"Type", "EFI"), new XElement(un+"Size", "260")),
                new XElement(un+"CreatePartition", new XAttribute(wcm+"action", "add"), new XElement(un+"Order", "2"), new XElement(un+"Type", "MSR"), new XElement(un+"Size", "16")),
                new XElement(un+"CreatePartition", new XAttribute(wcm+"action", "add"), new XElement(un+"Order", "3"), new XElement(un+"Type", "Primary"), new XElement(un+"Extend", "true"))),
            new XElement(un+"ModifyPartitions", new XElement(un+"ModifyPartition", new XAttribute(wcm+"action", "add"), new XElement(un+"Order", "1"), new XElement(un+"PartitionID", "1"), new XElement(un+"Format", "FAT32"), new XElement(un+"Label", "System")),
                new XElement(un+"ModifyPartition", new XAttribute(wcm+"action", "add"), new XElement(un+"Order", "2"), new XElement(un+"PartitionID", "3"), new XElement(un+"Format", "NTFS"), new XElement(un+"Label", "Windows"), new XElement(un+"Letter", "C")))), new XElement(un+"WillShowUI", "OnError"));
    }

    private string BuildPostScript()
    {
        var chosen = catalog.Where(option => GetFlag(option.Id)).OrderBy(option => option.Order).ToList();
        var selected = apps.Where(a => GetFlag("install:" + a.Id)).Select(a => a.Id).Distinct().ToList();
        selected.AddRange(GetField("customWingetIds", "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => Regex.IsMatch(x, "^[A-Za-z0-9][A-Za-z0-9._-]*$")));
        var activationKey = GetField("activationKey", "").Trim();
        if (chosen.Count == 0 && selected.Count == 0 && activationKey.Length == 0 && !GetFlag("selfDelete") && !GetFlag("createRestore")) return "";
        var lines = new List<string>
        {
            "#Requires -RunAsAdministrator",
            "$ErrorActionPreference = 'Continue'",
            "$logRoot = Join-Path $env:ProgramData 'UnattendStudio\\Logs'",
            "New-Item -Path $logRoot -ItemType Directory -Force | Out-Null",
            "Start-Transcript -LiteralPath (Join-Path $logRoot 'PostInstall.log') -Append | Out-Null",
            "function Set-OptionRegistry([string]$Path,[string]$Name,[string]$Type,[string]$Raw) { try { New-Item -Path $Path -Force | Out-Null; $v = switch ($Type) { 'DWord' { if ($Raw.StartsWith('0x')) { [Convert]::ToUInt32($Raw.Substring(2),16) } else { [Convert]::ToUInt32($Raw) } } 'QWord' { [Convert]::ToInt64($Raw) } 'Binary' { [Convert]::FromBase64String($Raw) } default { $Raw } }; New-ItemProperty -Path $Path -Name $Name -Value $v -PropertyType $Type -Force -ErrorAction Stop | Out-Null } catch { Write-Warning \"Registry setting failed: $Path\\$Name — $($_.Exception.Message)\" } }",
            "function Remove-OptionRegistryValue([string]$Path,[string]$Name) { Remove-ItemProperty -LiteralPath $Path -Name $Name -Force -ErrorAction SilentlyContinue }",
            "function Remove-OptionRegistryKey([string]$Path) { Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue }",
            "function Set-OptionBinaryBit([string]$Path,[string]$Name,[int]$Index,[string]$Mask,[bool]$Enable) { try { New-Item -Path $Path -Force | Out-Null; $b=(Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue).$Name; if ($null -eq $b -or $b.Length -le $Index) { $b=[byte[]]::new($Index+1) }; [byte]$m=if($Mask.StartsWith('0x')){[Convert]::ToByte($Mask.Substring(2),16)}else{[byte]$Mask}; if($Enable){$b[$Index]=$b[$Index] -bor $m}else{$b[$Index]=$b[$Index] -band ([byte]([byte]::MaxValue -bxor $m))}; Set-ItemProperty -Path $Path -Name $Name -Value ([byte[]]$b) -Force } catch { Write-Warning \"Binary flag failed: $Path\\$Name\" } }",
            "function Set-OptionBinaryByte([string]$Path,[string]$Name,[int]$Index,[string]$Raw) { try { New-Item -Path $Path -Force | Out-Null; $b=(Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue).$Name; if($null -eq $b -or $b.Length -le $Index){$b=[byte[]]::new($Index+1)}; [byte]$b[$Index]=if($Raw.StartsWith('0x')){[Convert]::ToByte($Raw.Substring(2),16)}else{[byte]$Raw}; Set-ItemProperty -Path $Path -Name $Name -Value ([byte[]]$b) -Force } catch { Write-Warning \"Binary byte failed: $Path\\$Name\" } }"
        };
        if (activationKey.Length > 0)
        {
            lines.Add($"$activationKey = {PsLiteral(activationKey)}");
            lines.Add("$slmgr = Join-Path $env:SystemRoot 'System32\\slmgr.vbs'; & (Join-Path $env:SystemRoot 'System32\\cscript.exe') //nologo $slmgr /ipk $activationKey");
            lines.Add("if ($LASTEXITCODE -ne 0) { Write-Warning 'Windows rejected the activation key; Windows Setup has already completed.' }");
            if (GetFlag("activateOnline")) lines.Add("& (Join-Path $env:SystemRoot 'System32\\cscript.exe') //nologo $slmgr /ato; if ($LASTEXITCODE -ne 0) { Write-Warning 'Online activation did not complete; run activation later from Windows Settings.' }");
        }
        if (GetFlag("createRestore")) AddRollbackPreparation(lines, chosen);
        foreach (var option in chosen)
        {
            switch (option.Action)
            {
                case "SetRegistry": lines.Add($"Set-OptionRegistry -Path {PsLiteral(option.Path)} -Name {PsLiteral(option.Name)} -Type {PsLiteral(option.RegistryType)} -Raw {PsLiteral(option.Value)}"); break;
                case "RemoveValue": lines.Add($"Remove-OptionRegistryValue -Path {PsLiteral(option.Path)} -Name {PsLiteral(option.Name)}"); break;
                case "RemoveKey": lines.Add($"Remove-OptionRegistryKey -Path {PsLiteral(option.Path)}"); break;
                case "NewKey": lines.Add($"New-Item -Path {PsLiteral(option.Path)} -Force | Out-Null"); break;
                case "BinaryBit": lines.Add($"Set-OptionBinaryBit -Path {PsLiteral(option.Path)} -Name {PsLiteral(option.Name)} -Index {option.ByteIndex} -Mask {PsLiteral(option.BitMask)} -Enable ${option.SetBit.ToString().ToLowerInvariant()}"); break;
                case "BinaryByte": lines.Add($"Set-OptionBinaryByte -Path {PsLiteral(option.Path)} -Name {PsLiteral(option.Name)} -Index {option.ByteIndex} -Raw {PsLiteral(option.Value)}"); break;
                case "ScheduledTask": AddScheduledTask(lines, option); break;
                case "PowerSetting": lines.Add($"powercfg /setacvalueindex SCHEME_CURRENT {option.Subgroup} {option.Setting} {option.Ac}; powercfg /setdcvalueindex SCHEME_CURRENT {option.Subgroup} {option.Setting} {option.Dc}; powercfg /S SCHEME_CURRENT"); break;
                case "PowerPlan": lines.Add(GetFlag("createRestore") ? "$schemes=@('e9a42b02-d5df-448d-aa00-03f14749eb61','8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c','381b4222-f694-41f0-9685-ff5bb260df2e'); foreach($scheme in $schemes){$out=powercfg /duplicatescheme $scheme 2>$null; if($LASTEXITCODE -eq 0){$guid=[regex]::Match(($out -join ' '),'[0-9a-fA-F-]{36}').Value; if($guid){powercfg /changename $guid 'Unattend Studio Performance'; powercfg /setactive $guid; $rollbackState.CreatedPlan=$guid; $rollbackState | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $stateFile -Encoding UTF8; break}}}" : "$schemes=@('e9a42b02-d5df-448d-aa00-03f14749eb61','8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c','381b4222-f694-41f0-9685-ff5bb260df2e'); foreach($scheme in $schemes){$out=powercfg /duplicatescheme $scheme 2>$null; if($LASTEXITCODE -eq 0){$guid=[regex]::Match(($out -join ' '),'[0-9a-fA-F-]{36}').Value; if($guid){powercfg /changename $guid 'Unattend Studio Performance'; powercfg /setactive $guid; break}}}"); break;
                case "HibernateOff": lines.Add("powercfg /hibernate off"); break;
                case "AppxPackage": AddAppxRemoval(lines, option.Package!); break;
                case "Capability": AddCapabilityRemoval(lines, option); break;
                case "CleanStart": lines.Add("if([Environment]::OSVersion.Version.Build -ge 22000){Set-OptionRegistry -Path 'HKLM:\\SOFTWARE\\Microsoft\\PolicyManager\\current\\device\\Start' -Name 'ConfigureStartPins' -Type 'String' -Raw '{\"pinnedList\":[]}'}else{$p='C:\\Users\\Default\\AppData\\Local\\Microsoft\\Windows\\Shell';New-Item -Path $p -ItemType Directory -Force|Out-Null;Set-Content -LiteralPath (Join-Path $p 'LayoutModification.xml') -Encoding UTF8 -Value '<LayoutModificationTemplate Version=\"1\" xmlns=\"http://schemas.microsoft.com/Start/2014/LayoutModification\"><DefaultLayoutOverride><StartLayoutCollection><StartLayout GroupCellWidth=\"6\" xmlns=\"http://schemas.microsoft.com/Start/2014/FullDefaultLayout\" /></StartLayoutCollection></DefaultLayoutOverride></LayoutModificationTemplate>'}"); break;
                case "EdgeRemoval": lines.Add("$edgeRoot=Join-Path ${env:ProgramFiles(x86)} 'Microsoft\\Edge\\Application'; $edgeSetup=Get-ChildItem (Join-Path $edgeRoot '*\\Installer\\setup.exe') -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1; if($edgeSetup){& $edgeSetup.FullName --uninstall --system-level --force-uninstall}"); break;
                case "OneDriveRemoval": lines.Add("$oneDriveSetup=Join-Path $env:SystemRoot 'SysWOW64\\OneDriveSetup.exe'; if(!(Test-Path $oneDriveSetup)){$oneDriveSetup=Join-Path $env:SystemRoot 'System32\\OneDriveSetup.exe'}; if(Test-Path $oneDriveSetup){Start-Process -FilePath $oneDriveSetup -ArgumentList '/uninstall' -Wait}"); break;
                case "RebootAfter": lines.Add("shutdown.exe /r /t 30 /c 'Unattend Studio: restart after selected optimizations'"); break;
            }
        }
        foreach (var id in selected.Distinct()) lines.Add($"if (Get-Command winget -ErrorAction SilentlyContinue) {{ winget install --exact --id {PsLiteral(id)} --silent --accept-package-agreements --accept-source-agreements }} else {{ Write-Warning 'winget not found; skipped {id}' }}");
        if (GetFlag("selfDelete")) lines.Add("$cleanup=Join-Path $env:TEMP 'unattend-self-delete.cmd'; $cleanupContent = \"@echo off`r`ntimeout /t 3 /nobreak >nul`r`ndel /f /q `\"$PSCommandPath`\"`r`ndel /f /q `\"%~f0`\"\"; Set-Content -LiteralPath $cleanup -Value $cleanupContent; Start-Process $cleanup -WindowStyle Hidden");
        lines.Add("Stop-Transcript | Out-Null");
        return string.Join("`r`n", lines) + "`r`n";
    }

    private static string PsLiteral(string? value) => "'" + (value ?? "").Replace("'", "''") + "'";
    private void AddRollbackPreparation(List<string> lines, IReadOnlyList<OptimizationOption> chosen)
    {
        var values = chosen.Where(option => option.Action is "SetRegistry" or "RemoveValue" or "BinaryBit" or "BinaryByte")
            .Select(option => new { Path = option.Path ?? "", Name = option.Name ?? "" }).Distinct().ToList();
        values.AddRange(chosen.Where(option => option.Action == "CleanStart").Select(_ => new { Path = @"HKLM:\SOFTWARE\Microsoft\PolicyManager\current\device\Start", Name = "ConfigureStartPins" }));
        var keys = chosen.Where(option => option.Action == "RemoveKey").Select(option => new { Path = option.Path ?? "" }).Distinct().ToList();
        var newKeys = chosen.Where(option => option.Action == "NewKey").Select(option => new { Path = option.Path ?? "" }).Distinct().ToList();
        var tasks = chosen.Where(option => option.Action == "ScheduledTask").Select(option =>
        {
            var full = option.TaskName ?? ""; var split = full.LastIndexOf('\\');
            return new { TaskPath = split >= 0 ? full[..(split + 1)] : "\\", TaskName = split >= 0 ? full[(split + 1)..] : full };
        }).Distinct().ToList();
        var power = chosen.Where(option => option.Action == "PowerSetting").Select(option => new { Subgroup = option.Subgroup ?? "", Setting = option.Setting ?? "" }).Distinct().ToList();
        var targetJson = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Values = values, Keys = keys, NewKeys = newKeys, Tasks = tasks, Power = power })));
        lines.Add("$rollbackRoot = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Unattend-Restore'; New-Item -Path $rollbackRoot -ItemType Directory -Force | Out-Null");
        lines.Add($"$rollbackTargets = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{targetJson}')) | ConvertFrom-Json");
        lines.Add("$rollbackState = [ordered]@{ RegistryValues=@(); RegistryKeys=@(); NewKeys=@(); Tasks=@(); Power=@(); ActiveScheme=$null; CreatedPlan=$null; HibernateEnabled=$null }");
        lines.Add("$activeText=powercfg /getactivescheme; $rollbackState.ActiveScheme=[regex]::Match(($activeText -join ' '),'[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}').Value");
        lines.Add("foreach($target in @($rollbackTargets.Values)){ $key=Get-Item -LiteralPath $target.Path -ErrorAction SilentlyContinue; $item=[ordered]@{Path=$target.Path;Name=$target.Name;KeyExisted=($null -ne $key);Exists=$false;Kind=$null;Value=$null;Binary64=$null}; if($null -ne $key -and $key.GetValueNames() -contains $target.Name){$item.Exists=$true;$item.Kind=$key.GetValueKind($target.Name).ToString();$item.Value=$key.GetValue($target.Name,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames);if($item.Kind -eq 'Binary'){$item.Binary64=[Convert]::ToBase64String($item.Value);$item.Value=$null}}; $rollbackState.RegistryValues+=,[pscustomobject]$item }");
        lines.Add("$keyIndex=0; foreach($target in @($rollbackTargets.Keys)){ $key=Get-Item -LiteralPath $target.Path -ErrorAction SilentlyContinue; if($null -ne $key){$file=Join-Path $rollbackRoot ('registry-key-'+$keyIndex+'.reg');$native=$target.Path -replace '^(HKLM|HKCU|HKCR|HKU|HKCC):\\\\','$1\\'; & reg.exe export $native $file /y *> $null; if($LASTEXITCODE -eq 0){$rollbackState.RegistryKeys+=,[pscustomobject]@{Path=$target.Path;File=$file}} else {throw ('Could not back up registry key: '+$native)}};$keyIndex++ }");
        lines.Add("foreach($target in @($rollbackTargets.NewKeys)){ $rollbackState.NewKeys+=,[pscustomobject]@{Path=$target.Path;Existed=($null -ne (Get-Item -LiteralPath $target.Path -ErrorAction SilentlyContinue))} }");
        lines.Add("foreach($target in @($rollbackTargets.Tasks)){ $task=Get-ScheduledTask -TaskPath $target.TaskPath -TaskName $target.TaskName -ErrorAction SilentlyContinue; if($null -ne $task){$rollbackState.Tasks+=,[pscustomobject]@{TaskPath=$target.TaskPath;TaskName=$target.TaskName;Enabled=($task.State -ne 'Disabled')}} }");
        lines.Add("foreach($target in @($rollbackTargets.Power)){ $result=powercfg /query $rollbackState.ActiveScheme $target.Subgroup $target.Setting 2>$null; $text=$result -join \"`n\"; $ac=[regex]::Match($text,'Current AC Power Setting Index:\\s*(0x[0-9a-fA-F]+)').Groups[1].Value; $dc=[regex]::Match($text,'Current DC Power Setting Index:\\s*(0x[0-9a-fA-F]+)').Groups[1].Value; if($ac -and $dc){$rollbackState.Power+=,[pscustomobject]@{Scheme=$rollbackState.ActiveScheme;Subgroup=$target.Subgroup;Setting=$target.Setting;AC=$ac;DC=$dc}} }");
        lines.Add("$hibernate=(Get-ItemProperty -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Power' -Name HibernateEnabled -ErrorAction SilentlyContinue).HibernateEnabled; if($null -ne $hibernate){$rollbackState.HibernateEnabled=[int]$hibernate}");
        lines.Add("$stateFile=Join-Path $rollbackRoot 'state.json'; $rollbackState | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $stateFile -Encoding UTF8");
        var restore64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(RestoreScript()));
        lines.Add($"$restoreBytes=[Convert]::FromBase64String('{restore64}'); [IO.File]::WriteAllBytes((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Unattend-Restore.ps1'),$restoreBytes)");
        lines.Add("Write-Host ('Rollback snapshot saved to ' + $rollbackRoot)");
    }

    private static string RestoreScript() => """
#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'Unattend-Restore'
$statePath = Join-Path $root 'state.json'
if (!(Test-Path -LiteralPath $statePath)) { throw 'Rollback state was not found.' }
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
foreach ($entry in @($state.RegistryKeys)) {
    if (Test-Path -LiteralPath $entry.Path) { Remove-Item -LiteralPath $entry.Path -Recurse -Force }
    if (Test-Path -LiteralPath $entry.File) { & reg.exe import $entry.File | Out-Null }
}
foreach ($entry in @($state.RegistryValues)) {
    if ($entry.Exists) {
        New-Item -Path $entry.Path -Force | Out-Null
        Remove-ItemProperty -LiteralPath $entry.Path -Name $entry.Name -Force -ErrorAction SilentlyContinue
        $value = if ($entry.Kind -eq 'Binary') { [Convert]::FromBase64String($entry.Binary64) } else { $entry.Value }
        New-ItemProperty -LiteralPath $entry.Path -Name $entry.Name -PropertyType $entry.Kind -Value $value -Force | Out-Null
    } else { Remove-ItemProperty -LiteralPath $entry.Path -Name $entry.Name -Force -ErrorAction SilentlyContinue }
}
foreach ($entry in @($state.RegistryValues | Where-Object { !$_.KeyExisted })) { Remove-Item -LiteralPath $entry.Path -Recurse -Force -ErrorAction SilentlyContinue }
foreach ($entry in @($state.Tasks)) {
    if ($entry.Enabled) { Enable-ScheduledTask -TaskPath $entry.TaskPath -TaskName $entry.TaskName -ErrorAction SilentlyContinue | Out-Null }
    else { Disable-ScheduledTask -TaskPath $entry.TaskPath -TaskName $entry.TaskName -ErrorAction SilentlyContinue | Out-Null }
}
foreach ($entry in @($state.Power)) {
    powercfg /setacvalueindex $entry.Scheme $entry.Subgroup $entry.Setting $entry.AC | Out-Null
    powercfg /setdcvalueindex $entry.Scheme $entry.Subgroup $entry.Setting $entry.DC | Out-Null
}
if ($state.ActiveScheme) { powercfg /setactive $state.ActiveScheme | Out-Null }
if ($state.CreatedPlan) { powercfg /delete $state.CreatedPlan | Out-Null }
if ($null -ne $state.HibernateEnabled) { if ($state.HibernateEnabled -eq 1) { powercfg /hibernate on | Out-Null } else { powercfg /hibernate off | Out-Null } }
foreach ($entry in @($state.NewKeys | Where-Object { !$_.Existed })) { Remove-Item -LiteralPath $entry.Path -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host 'Reversible registry, task, and power settings were restored. Removed apps/features are not restored.'
""";

    private static void AddScheduledTask(List<string> lines, OptimizationOption option)
    {
        var full = option.TaskName ?? ""; var split = full.LastIndexOf('\\');
        var taskPath = split >= 0 ? full[..(split + 1)] : "\\"; var taskName = split >= 0 ? full[(split + 1)..] : full;
        lines.Add($"Disable-ScheduledTask -TaskPath {PsLiteral(taskPath)} -TaskName {PsLiteral(taskName)} -ErrorAction SilentlyContinue | Out-Null");
    }
    private static void AddAppxRemoval(List<string> lines, string package)
    {
        var literal = PsLiteral(package);
        lines.Add($"Get-AppxPackage -AllUsers -Name {literal} -ErrorAction SilentlyContinue | ForEach-Object {{ Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction SilentlyContinue }}");
        lines.Add($"Get-AppxProvisionedPackage -Online | Where-Object {{$_.DisplayName -eq {literal}}} | ForEach-Object {{ Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue | Out-Null }}");
    }
    private static void AddCapabilityRemoval(List<string> lines, OptimizationOption option)
    {
        var literal = PsLiteral(option.Package);
        if (option.Kind == "feature") lines.Add($"Get-WindowsOptionalFeature -Online -FeatureName {literal} -ErrorAction SilentlyContinue | Where-Object State -eq 'Enabled' | Disable-WindowsOptionalFeature -Online -NoRestart -ErrorAction SilentlyContinue | Out-Null");
        else lines.Add($"Get-WindowsCapability -Online | Where-Object Name -like ({literal} + '*') | ForEach-Object {{ Remove-WindowsCapability -Online -Name $_.Name -ErrorAction SilentlyContinue | Out-Null }}");
    }
    private string BuildBootstrap(string script)
    {
        byte[] compressed;
        using (var output = new MemoryStream())
        {
            using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            using (var writer = new StreamWriter(gzip, new UTF8Encoding(false))) writer.Write(script);
            compressed = output.ToArray();
        }
        var script64 = Convert.ToBase64String(compressed);
        var run = GetFlag("autoRun") ? "Start-Process powershell.exe -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',$path) -Wait" : "";
        return $$"""
$ErrorActionPreference='Stop'; $path=Join-Path ([Environment]::GetFolderPath('Desktop')) 'Unattend-PostInstall.ps1'; $bytes=[Convert]::FromBase64String('{{script64}}'); $inputStream=[IO.MemoryStream]::new($bytes); $gzip=[IO.Compression.GzipStream]::new($inputStream,[IO.Compression.CompressionMode]::Decompress); $reader=[IO.StreamReader]::new($gzip,[Text.Encoding]::UTF8); $content=$reader.ReadToEnd(); $reader.Dispose(); $gzip.Dispose(); $inputStream.Dispose(); Set-Content -LiteralPath $path -Value $content -Encoding UTF8; {{run}}
""";
    }

    private (List<string> Errors, List<string> Warnings) ValidateConfiguration()
    {
        var errors = new List<string>(); var warnings = new List<string>();
        var username = GetField("username", "").Trim(); var password = GetField("password", "");
        if (GetFlag("account") && (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))) errors.Add(L("userRequired"));
        if (GetFlag("account") && (username.Length > 20 || Regex.IsMatch(username, "[\\\\/\\[\\]:;|=,+*?<>\\\"]"))) errors.Add(L("usernameInvalid"));
        if (GetField("accountType", "Administrators") is not ("Administrators" or "Users")) errors.Add(L("accountType"));
        var imageMode = GetField("imageSelectionMode", "edition");
        if (imageMode is not ("edition" or "name" or "index" or "description")) errors.Add(L("imageValueRequired"));
        if (imageMode != "edition" && string.IsNullOrWhiteSpace(GetField("imageSelectorValue", ""))) errors.Add(L("imageValueRequired"));
        if (imageMode == "index" && (!uint.TryParse(GetField("imageSelectorValue", ""), out var imageIndex) || imageIndex == 0)) errors.Add(L("imageIndexInvalid"));
        if (!IsXmlSafe(GetField("imageSelectorValue", "")) || !IsXmlSafe(username) || !IsXmlSafe(password) || !IsXmlSafe(GetField("fullName", "")) || !IsXmlSafe(GetField("organization", ""))) errors.Add(L("xmlCharactersInvalid"));
        var key = GetField("activationKey", "").Trim();
        if (key.Length > 0 && !Regex.IsMatch(key, "^[A-Za-z0-9]{5}(?:-[A-Za-z0-9]{5}){4}$")) errors.Add(L("activationKeyInvalid"));
        if (GetFlag("activateOnline") && key.Length == 0) warnings.Add(L("noActivationKey"));
        if (key.Length > 0 && !GetFlag("autoRun")) warnings.Add(L("activationManual"));
        if (GetFlag("wipeDisk") && !uint.TryParse(GetField("targetDisk", "0"), out _)) errors.Add(L("diskInvalid"));
        var computer = GetField("computer", "").Trim();
        if (computer.Length > 0 && (!Regex.IsMatch(computer, "^[A-Za-z0-9-]{1,15}$") || Regex.IsMatch(computer, "^[0-9]+$"))) errors.Add(L("computerInvalid"));
        if (GetFlag("account") && GetField("accountType", "Administrators") == "Users" && !string.IsNullOrWhiteSpace(BuildPostScript())) warnings.Add(L("postNeedsAdmin"));
        return (errors, warnings);
    }

    private static bool IsXmlSafe(string value)
    {
        try { XmlConvert.VerifyXmlChars(value); return true; } catch (XmlException) { return false; }
    }

    private bool ConfirmConfiguration()
    {
        var validation = ValidateConfiguration();
        if (validation.Errors.Count > 0)
        {
            MessageBox.Show(this, L("validationErrors") + "\n\n• " + string.Join("\n• ", validation.Errors), L("validationTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        if (validation.Warnings.Count > 0 && MessageBox.Show(this, L("validationWarnings") + "\n\n• " + string.Join("\n• ", validation.Warnings), L("validationTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) != MessageBoxResult.OK) return false;
        return true;
    }

    private void SaveXml()
    {
        if (!ConfirmConfiguration()) return;
        if (GetFlag("wipeDisk") && MessageBox.Show(this, L("confirmWipe").Replace("{disk}", GetField("targetDisk", "0")), L("confirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var criticalOptions = catalog.Where(option => GetFlag(option.Id) && OptionRisk(option) == "critical").Select(OptionDisplayLabel).ToList();
        if (criticalOptions.Count > 0 && MessageBox.Show(L("confirmOptimization") + "\n\n• " + string.Join("\n• ", criticalOptions), L("critical"), MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
        if (GetFlag("account") && MessageBox.Show(L("accountHint"), L("confirmTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        if (!string.IsNullOrWhiteSpace(GetField("activationKey", "")) && MessageBox.Show(this, L("activationXmlWarn"), L("confirmTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
        var dialog = new SaveFileDialog { FileName = "autounattend.xml", Filter = "XML file (*.xml)|*.xml", DefaultExt = ".xml" };
        if (dialog.ShowDialog() != true) return;
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
        using (var writer = XmlWriter.Create(dialog.FileName, settings)) GenerateXml().Save(writer);
        status.Text = L("created");
    }
    private void SaveProfile()
    {
        if ((GetFlag("account") || !string.IsNullOrWhiteSpace(GetField("password", "")) || !string.IsNullOrWhiteSpace(GetField("activationKey", ""))) && MessageBox.Show(this, L("profileWarn"), L("saveProfile"), MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;
        var dlg = new SaveFileDialog { Filter = "Unattend profile (*.json)|*.json", FileName = "unattend-profile.json" };
        if (dlg.ShowDialog() != true) return; File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true })); status.Text = L("created");
    }
    private void LoadProfile()
    {
        var dlg = new OpenFileDialog { Filter = "Unattend profile (*.json)|*.json|All files (*.*)|*.*" }; if (dlg.ShowDialog() != true) return;
        try { state = JsonSerializer.Deserialize<UserConfig>(File.ReadAllText(dlg.FileName)) ?? new(); BuildUi(); status.Text = L("profileLoaded"); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Unattend Studio", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void LoadPreferences()
    {
        try { var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnattendStudio", "preferences.json"); if (File.Exists(path)) { var prefs = JsonSerializer.Deserialize<UserConfig>(File.ReadAllText(path)); if (prefs != null) { state.Language = languages.Contains(prefs.Language) ? prefs.Language : "pt-BR"; state.Dark = !prefs.ThemePreferenceSet || prefs.Dark; state.ThemePreferenceSet = prefs.ThemePreferenceSet; } } } catch { }
    }
    private void SavePreferences()
    {
        try { var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UnattendStudio"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "preferences.json"), JsonSerializer.Serialize(new UserConfig { Language = state.Language, Dark = state.Dark, ThemePreferenceSet = state.ThemePreferenceSet })); } catch { }
    }
}
