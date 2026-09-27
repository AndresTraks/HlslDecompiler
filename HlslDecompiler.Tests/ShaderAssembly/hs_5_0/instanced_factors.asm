hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_fractional_odd
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
hs_fork_phase
dcl_hs_fork_phase_instance_count 3
dcl_input vForkInstanceID
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_temps 1
dcl_indexrange o0 3
mov r0.x, vForkInstanceID.x
mov o[r0.x].x, cb0[0].x
ret
hs_fork_phase
dcl_output_siv o3.x, finalTriInsideTessFactor
mov o3.x, cb0[0].x
ret
