# SimpleKits

Plugin de kits de itens para servidores Unturned (Rocket 4.x / .NET Framework 4.8) com interface gráfica completa no jogo.

![Preview](KitsUI-preview.html)

## Funcionalidades

- **UI no jogo** (efeito 47501): lista de kits com cards, editor de kits (admin) e baú virtual para montar os kits clicando nos itens do inventário.
- **Cooldown** por kit e por jogador, persistido em `Cooldowns.json`.
- **Permissões** por kit (`kits.<nome>`); `kits.admin` dá acesso total (criar/editar/deletar, claim sem cooldown).
- **Auto-equip** (por jogador, persistido em `PlayerSettings.json`): equipa arma/roupas automaticamente ao resgatar.
- **Pegar sem espaço** (por jogador): `OFF` bloqueia o resgate e não consome o cooldown se faltar espaço; `ON` entrega o que couber e dropa o restante no chão.
- **Armas modificadas preservadas**: acessórios (mira, tático, grip, cano, pente) e munição são salvos no state do item e restaurados no resgate.
- **Ícones de itens** carregados por URL (`ItemIconUrlTemplate`, padrão: Akulation/vanilla-icons).
- **Módulo opcional do cliente** (`UnturnedItemIcons`): exporta ícones de itens e permite fechar a UI com **ESC**.

## Requisitos

- Unturned dedicado (cliente/servidor com Unity 2022.3+)
- [Rocket.Unturned](https://github.com/RocketMod/Rocket.Unturned) 4.x (módulo `Rocket.Unturned`)
- .NET Framework 4.8 (Windows) para compilar

## Instalação (servidor)

1. **Assine o item da Workshop** [Simple Kits UI](https://steamcommunity.com/sharedfiles/filedetails/?id=3782829202) (efeito 47501) **e adicione o ID `3782829202` ao `WorkshopDownloadConfig.json`** do servidor.
   - Alternativa sem Workshop: copie a pasta `KitsUI.unity3d` + `KitsUI.dat` para `Servers/<id>/Workshop/Content/3782829202/Effects/KitsUI/`.
2. Baixe o Rocket.Unturned e coloque em `Modules/Rocket.Unturned/` do servidor.
3. Copie `SimpleKits.dll` (build da pasta `SimpleKits/`) para:
   - `Servers/<id>/Rocket/Plugins/SimpleKits.dll`
   - `Rocket/Plugins/SimpleKits.dll`
4. Opcional: copie `Translations/SimpleKits.en.translation.xml` para `Servers/<id>/Rocket/Plugins/SimpleKits/`.
5. Inicie o servidor; o plugin gera `SimpleKits.configuration.xml` automaticamente na primeira carga.

## Permissões

| Permissão | Efeito |
|---|---|
| `kits.admin` (padrão do `BypassPermission`) | Cria/edita/deleta kits, baú virtual, claim sem cooldown |
| `kits.<nome>` | Necessária para resgatar o kit (se configurada) |

## Comandos

| Comando | Descrição |
|---|---|
| `/kits` | Abre a UI de kits |
| `/kit <nome>` | Resgata o kit pelo nome |
| `/kitsadmin` (alias `/kits admin`) | Comandos de administração (add/remove/additem/removeitem/set) |

## Configuração (`SimpleKits.configuration.xml`)

| Campo | Descrição |
|---|---|
| `BypassPermission` | Permissão de admin (padrão `kits.admin`) |
| `EffectId` | ID do efeito da UI (padrão `47501`) |
| `ServerIconURL` | Imagem opcional exibida no topo da UI |
| `ItemIconUrlTemplate` | Template de URL dos ícones; `{0}` = ID do item |
| `VirtualVaultOnly` | `true` = baú virtual (clicar itens do inventário); `false` = baú do jogo |
| `Kits/Kit` | Lista de kits: `Name`, `CooldownSeconds`, `Permission`, `Priority`, `Items/Item` (`ItemID`, `Amount`, `State` opcional em base64) |

## Construindo

```bat
:: Plugin
dotnet build SimpleKits\SimpleKits.csproj -c Release

:: UI (Unity 2022.3.62f3) — abre KitsUI-Unity na Unity e executa o menu
:: KitsUI > Build Effect Asset (server + workshop), ou via batchmode:
Unity.exe -batchmode -quit -projectPath KitsUI-Unity -executeMethod KitUiBuilder.BuildAndStage
```

## Créditos

- Ícones de itens: [Akulation/vanilla-icons](https://github.com/Akulation/vanilla-icons)
- UI construída com Unity uGUI (efeito carregado via `EffectManager`)

## Licença

MIT — veja [LICENSE](LICENSE).
