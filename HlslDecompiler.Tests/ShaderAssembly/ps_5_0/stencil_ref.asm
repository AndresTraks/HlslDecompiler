ps_5_0
dcl_globalFlags refactoringAllowed
dcl_input_ps_siv linear noperspective v0, position
dcl_output o0
dcl_output oStencilRef
mov o0, v0
ftou oStencilRef, v0.x
ret
