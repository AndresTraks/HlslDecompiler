ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 1
ieq r0.x, v1.x, l(-1)
if_nz r0.x
mov o0.x, l(1)
mov o0.yzw, v0.yzw
ret
else
ieq r0.x, v1.x, l(3)
if_nz r0.x
mov o0.xzw, v0.xzw
mov o0.y, l(2)
else
ieq r0.x, v1.x, l(4)
if_nz r0.x
mov o0, l(0, 0, 0, 0)
ret
endif
mov o0, v0.wzyx
endif
ret
endif
ret
