ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
and r0.x, v0.w, l(2139095040)
iadd r0.x, r0.x, l(-1056964608)
ne r0.y, v0.w, l(0)
and r0.x, r0.y, r0.x
ishr r0.x, r0.x, l(23)
itof r0.x, r0.x
bfi r0.z, l(23), l(0), v0.w, l(1056964608)
and r0.y, r0.y, r0.z
add o0.w, r0.x, r0.y
mul r0.x, v0.z, l(0.333333343)
ge r0.y, r0.x, -r0.x
frc r0.x, |r0.x|
movc r0.x, r0.y, r0.x, -r0.x
exp r0.y, v0.y
mul r0.y, r0.y, v0.x
mad o0.z, r0.x, l(3), r0.y
sincos r0.x, r1.x, v0.x
add o0.x, r0.x, r1.x
mov o0.y, v0.y
ret
