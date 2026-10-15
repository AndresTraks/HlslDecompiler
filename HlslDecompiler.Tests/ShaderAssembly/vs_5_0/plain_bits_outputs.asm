vs_5_0
dcl_globalFlags refactoringAllowed
dcl_input v0
dcl_input_sgv v1.x, vertex_id
dcl_output_siv o0, position
dcl_output o1.xy
dcl_output o1.z
mov o0, v0
mov o1.xz, v0.xw
mov o1.y, v1.x
ret
