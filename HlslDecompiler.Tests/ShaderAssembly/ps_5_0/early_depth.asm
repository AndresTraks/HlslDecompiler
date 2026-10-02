ps_5_0
dcl_globalFlags refactoringAllowed | forceEarlyDepthStencil
dcl_input_ps_siv linear noperspective v0, position
dcl_output o0
dcl_temps 1
add r0.x, v0.w, l(-0.5)
lt r0.x, r0.x, l(0)
discard_nz r0.x
add o0, v0, v0
ret
