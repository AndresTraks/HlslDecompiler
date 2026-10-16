ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 1
ieq r0.x, v1.x, l(3)
movc r0.x, r0.x, l(7), v0.y
ult r0.y, l(5), v1.x
if_nz r0.y
mov o0.xzw, v0.wyw
mov o0.y, r0.x
ret
else
mov o0.xz, v0.xz
mov o0.y, r0.x
mov o0.w, l(1)
ret
endif
ret
