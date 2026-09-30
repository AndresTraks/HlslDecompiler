ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps_siv linear noperspective v0.x, position
dcl_output o0
dcl_temps 2
and r0.x, v0.x, l(2139095040)
iadd r0.x, r0.x, l(-1056964608)
ne r0.y, v0.x, l(0)
and r0.x, r0.x, r0.y
ishr r0.x, r0.x, l(23)
itof r1.y, r0.x
exp r0.x, r1.y
bfi r0.z, l(23), l(0), v0.x, l(1056964608)
and r0.y, r0.z, r0.y
itof r1.x, r0.y
ishl r0.y, r0.y, l(3)
itof o0.z, r0.y
mul o0.w, r0.x, r1.x
mov o0.xy, r1.xy
ret
