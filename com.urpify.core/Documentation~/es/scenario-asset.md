# Convertir un paquete de la Asset Store para ambas pipelines

Publicas shaders o efectos en la Asset Store y quieres un único paquete que funcione para compradores con Built-in y con
URP. La acción principal es **Exportar para Built-in y URP** (Pro).

## Qué necesitas

- Un proyecto con URP instalado. Los shaders convertidos se compilan para URP antes de escribirse, así que la exportación
  se niega a ejecutarse sin él. Tu paquete en sí puede seguir siendo un paquete de Built-in; el proyecto solo aloja la
  exportación.
- El `.unitypackage` que distribuyes hoy.

## Analizar el paquete

En la ventana de Urpify: **Cambiar** (ámbito) > **Paquete de Unity (.unitypackage)...** y elige tu archivo. Urpify lo
importa en una carpeta temporal, lo analiza y borra la carpeta cuando cierras la ventana o cambias el ámbito. Tu proyecto
se queda como estaba.

El resumen muestra qué se rompe para un comprador con URP, igual que para un proyecto. El paquete gratuito se detiene
aquí: el informe (Informe > Exportar HTML) es lo que puedes mostrar o adjuntar.

## Exportar

En el resumen, **Exportar para Built-in y URP...**, o Informe > **Exportar paquete convertido...**. Elige dónde
guardarlo. El nuevo `.unitypackage` conserva cada GUID y cada ruta del original y contiene:

- los shaders convertidos. Con la salida **Mantener compatibilidad con Built-in** (Project Settings > Urpify > Conversión >
  Salida), cada archivo conserva sin cambios sus SubShaders originales de Built-in y lleva delante un SubShader de URP,
  así que un archivo funciona en ambas pipelines; Unity elige el SubShader que corresponde a la pipeline activa;
- los materiales que usaban shaders integrados de Unity, actualizados como lo haría el conversor de Unity en el proyecto
  del comprador, con las correcciones de Urpify después;
- los scripts convertidos como en un proyecto, cada uno compilado contra el proyecto antes de escribirse. Un script cuya
  reescritura no compila se queda como estaba y aparece en la lista;
- la biblioteca de shaders de Urpify y su pequeña parte de runtime, en una carpeta `UrpCompat` dentro de cada carpeta raíz
  de tu paquete, con GUIDs nuevos en cada exportación y el runtime en el namespace de tu paquete. Dos paquetes de autores
  distintos nunca comparten ni sobrescriben la copia del otro;
- una pequeña comprobación del Editor, con el nombre de tu paquete. Cuando el proyecto del comprador usa URP, revisa los
  URP Assets en busca de lo que necesitan tus shaders (Depth Texture, Opaque Texture, el Renderer Feature Built-in Camera
  Hooks) y pregunta una vez antes de activar nada. En Built-in no hace nada.

Junto al paquete se guarda un informe de compatibilidad: cada shader con su resultado y los ajustes del URP Asset que
necesitan los compradores.

## Comprobarlo como un comprador

Importa el paquete exportado en un proyecto limpio de Built-in y en un proyecto limpio de URP. En Built-in, la salida
Mantener compatibilidad con Built-in se renderiza exactamente como el original: los SubShaders originales no se tocan. En
URP, abre la escena de demostración del paquete y compárala con tus capturas de Built-in, o ejecuta sobre ella la
comparación de escenas de la migración.

## Redistribución

Puedes distribuir los shaders convertidos y la carpeta `UrpCompat` en tus paquetes de pago. Consulta
[Redistribuir shaders convertidos](../license-redistribution.md) (en inglés).
