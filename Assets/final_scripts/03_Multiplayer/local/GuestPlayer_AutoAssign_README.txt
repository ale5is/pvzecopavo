GUEST PLAYER - ASIGNACION AUTOMATICA

Comportamiento:
- Jugadores 2, 3 y 4 empiezan desactivados.
- Jugador 1 sigue usando mouse.
- No existe un jugador fijo para WASD, flechas o mando.
- El primer input que pulse su boton de activacion toma el siguiente slot libre entre Jugador 2, 3 y 4.
- El input queda vinculado a ese slot.
- Su boton de desactivacion elimina solamente ese Guest y libera ese slot.

Valores por defecto editables desde GuestPlayerManager:
- Gamepad: Start activa / Select desactiva.
- WASD: Z activa / X desactiva.
- Flechas: Enter activa / Backspace desactiva.

Ejemplo:
1. WASD pulsa Z -> Jugador 2.
2. Flechas pulsa Enter -> Jugador 3.
3. Gamepad pulsa Start -> Jugador 4.
4. WASD pulsa X -> solo Jugador 2 se desactiva.
5. Otro input que active pasa a ocupar el siguiente slot libre.

Los botones de activacion/desactivacion se cambian desde el Inspector.
Los perfiles de movimiento, cartas, confirmar y cancelar siguen siendo configurables por GuestControlProfile.

Si un mando se desconecta, su Guest se desactiva y el slot queda libre.
