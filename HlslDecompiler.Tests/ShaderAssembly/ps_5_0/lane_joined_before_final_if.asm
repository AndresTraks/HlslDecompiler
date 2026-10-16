ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 1
ieq r0.x, v1.x, l(3)
if_nz r0.x
mov o0.y, l(7)
else
mov o0.y, v0.y
endif
ult r0.x, l(5), v1.x
if_nz r0.x
mov o0.xzw, v0.wyw
else
mov o0.xz, v0.xz
mov o0.w, l(1)
endif
ret
