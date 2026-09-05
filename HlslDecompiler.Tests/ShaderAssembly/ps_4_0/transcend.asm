ps_4_0
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
mul r0.x, v0.x, l(1.44269502)
Exp
Log
mad r0.x, r0.y, l(0.693147182), r0.x
Log
mul r1, r1, l(2.5, 2.5, 2.5, 2.5)
Exp
add r0, r0.x, r1
sqrt r1.x, v0.z
add r0, r0, r1.x
Frc
add o0, r0, r1.x
ret
