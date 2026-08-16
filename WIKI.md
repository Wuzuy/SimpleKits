# SimpleKits — Wiki

Plugin de kits de itens para servidores Unturned com interface gráfica completa no jogo.

## O que o plugin tem

### Kits
- **Resgate** pela UI (`/kits`) ou comando (`/kit <nome>`).
- **Cooldown** individual por kit e jogador, persistido entre restarts (`Cooldowns.json`).
- **Permissões** por kit (`kits.<nome>`); a permissão de bypass (`kits.admin` por padrão) permite administrar e resgatar sem cooldown.
- **Prioridade**: kits com prioridade maior aparecem primeiro na lista.
- **Ícone do kit**: por padrão usa o ícone do primeiro item; o admin pode definir um **link de imagem customizado** (campo `Ícone do kit (URL)` no editor ou `/kits admin set <nome> icon <url>`; use `-` para limpar).

### Entrega inteligente
- **Quantidade correta**: itens não empilháveis são entregues individualmente; empilháveis em stacks válidas.
- **Armas modificadas preservadas**: acessórios (mira, tático, grip, cano, pente) e munição são salvos no `State` do item e restaurados no resgate. O kit guarda 1 pente com a quantidade de bala da arma.
- **Auto-equip** (por jogador): equipa arma/roupas automaticamente ao resgatar.
- **Pegar sem espaço** (por jogador):
  - `OFF`: se faltar espaço, o resgate é **bloqueado** e o cooldown **não** é consumido.
  - `ON`: entrega o que couber e **dropa o restante no chão** (cooldown é consumido).

### UI no jogo (efeito 47501)
- **Lista de kits** com cards (título, ícone, conteúdo, cooldown, CLAIM / VISUALIZAR).
- **Visualizar** (botão VISUALIZAR em cada card):
  - Clique esquerdo: grade com os itens do kit.
  - Clique direito: mostra os **detalhes exatos das armas** — mira, tático, grip, cano, pente e munição (ex.: "Mira: 8x Scope", "Pente: Military Drum", "Munição: 25/30").
  - Jogadores só veem kits com permissão.
- **Editor de kits** (admin): nome, itens (IDxQtd), cooldown, prioridade, permissão e ícone customizado.
- **Baú virtual** (admin): clique nos itens do inventário para montar o kit; munição/pente entra inteiro em 1 clique; a aba "NO KIT" devolve itens.
- **Configurações** (engrenagem ⚙ no topo): Auto-equip e Pegar sem espaço, cada um com botão "?" que explica o que faz ON/OFF (popup no próprio painel).
- **ESC** fecha a UI; **crosshair e UI de vida são ocultados** enquanto a UI está aberta.
- **Módulo do cliente** (`UnturnedItemIcons`) responsável por: ESC, ocultar o crosshair de armas e o clique direito no VISUALIZAR.

### Persistência
- `Cooldowns.json` e `PlayerSettings.json` (auto-equip + pegar sem espaço por steamID).

## Demonstração

Abra **`https://wuzuy.github.io/SimpleKits/`** (GitHub Pages) para ver a UI em HTML (abas: Lista, Editor, Baú, Configurações). No jogo: `/kits` abre a interface.

## Instalação em um servidor

### Requisitos
- Unturned dedicado (Windows) com o módulo [Rocket.Unturned](https://github.com/RocketMod/Rocket.Unturned) 4.x.
- .NET Framework 4.8 (para compilar; o servidor já roda com ele).

### Passos
1. **Workshop**: assine o item [Simple Kits UI](https://steamcommunity.com/sharedfiles/filedetails/?id=3782829202) e adicione o ID `3782829202` no `Servers/<id>/WorkshopDownloadConfig.json`:
   ```json
   { "File_IDs": ["3782829202"] }
   ```
   Sem Workshop: copie `Content/Effects/KitsUI/` (KitsUI.unity3d + KitsUI.dat) para `Servers/<id>/Workshop/Content/3782829202/Effects/KitsUI/`.
2. Coloque `Rocket.Unturned` em `Modules/Rocket.Unturned/` do servidor.
3. Copie `SimpleKits.dll` (compile com `dotnet build SimpleKits/SimpleKits.csproj -c Release`) para:
   - `Servers/<id>/Rocket/Plugins/SimpleKits.dll`
   - `Rocket/Plugins/SimpleKits.dll`
4. (Opcional) Copie `SimpleKits/Translations/SimpleKits.en.translation.xml` para `Servers/<id>/Rocket/Plugins/SimpleKits/`.
5. Inicie o servidor — o plugin gera `SimpleKits.configuration.xml` na primeira carga.

### Cliente (opcional, recomendado)
- Copie `UnturnedItemIcons.dll` (compile em `UnturnedItemIcons/`) para `Unturned/Modules/UnturnedItemIcons/` do **cliente** (fornece ESC, crosshair e clique direito).

## Permissões

| Permissão | Efeito |
|---|---|
| `kits.admin` (padrão do `BypassPermission`) | Admin: cria/edita/deleta kits, baú virtual, claim sem cooldown |
| `kits.<nome>` | Necessária para resgatar o kit (se configurada) |

## Comandos

| Comando | Descrição |
|---|---|
| `/kits` | Abre a UI |
| `/kit <nome>` | Resgata pelo nome |
| `/kitsadmin add <nome> [cooldown] [prioridade] [permissao]` | Cria kit |
| `/kitsadmin remove <nome>` | Remove kit |
| `/kitsadmin additem <nome> <itemID> [qtd]` | Adiciona item |
| `/kitsadmin removeitem <nome> <itemID>` | Remove item |
| `/kitsadmin set <nome> <cooldown\|prioridade\|permissao\|nome\|icon> <valor>` | Altera campos (icon `-` limpa) |
| `/kitsadmin list` | Lista kits |

## Configuração (`SimpleKits.configuration.xml`)

```xml
<KitConfiguration>
  <BypassPermission>kits.admin</BypassPermission>
  <EffectId>47501</EffectId>
  <ServerIconURL />
  <ItemIconUrlTemplate>https://cdn.jsdelivr.net/gh/Akulation/vanilla-icons@main/icons/{0}.png</ItemIconUrlTemplate>
  <VirtualVaultOnly>true</VirtualVaultOnly>
  <Kits>
    <Kit Name="start">
      <CooldownSeconds>30</CooldownSeconds>
      <Priority>0</Priority>
      <Items>
        <Item><ItemID>328</ItemID><Amount>1</Amount></Item>
      </Items>
    </Kit>
  </Kits>
</KitConfiguration>
```

- `ItemIconUrlTemplate`: `{0}` = ID do item (fonte padrão: Akulation/vanilla-icons).
- `Item State` (atributo opcional, base64): armas com acessórios/munição — gerado automaticamente pelo baú virtual.
- `Kit IconUrl` (atributo opcional): ícone customizado do kit.

## FAQ

- **O ícone do kit está errado?** Se `IconUrl` do kit estiver definido, ele substitui o ícone do 1º item.
- **Não consigo resgatar**: verifique permissão e cooldown; se o inventário estiver cheio e "Pegar sem espaço" estiver OFF, o resgate é bloqueado (sem consumir cooldown).
- **Arma resgatada sem os acessórios**: o item precisa ter sido depositado pelo **baú virtual** (o State só é salvo quando o item entra pelo baú).
- **UI invisível no servidor**: confirme o `EffectId` (47501) e o bundle em `Servers/<id>/Workshop/Content/3782829202/Effects/KitsUI/`.

## Licença

MIT.
